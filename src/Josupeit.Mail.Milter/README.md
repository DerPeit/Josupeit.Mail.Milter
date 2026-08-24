# Josupeit.Mail.Milter

An implementation of the **Milter protocol** - the mail filter protocol that Postfix and Sendmail speak - for .NET.

An MTA connects to a milter over a socket and streams the SMTP conversation to it as it happens: the client connecting, `HELO`, `MAIL FROM`, each `RCPT TO`, every header, the body. After each event the filter answers, and the answer decides what the MTA does next - carry on, accept the message outright, reject it, defer it, or discard it silently. At the end of the body the filter may also rewrite the message: add, insert or change headers, add or remove recipients, replace the envelope sender or the body, or quarantine the whole thing.

This package lets you write that filter in C#. You implement the callbacks for the events you care about; the library handles the framing, the option negotiation and the protocol state machine the MTA expects.

```
dotnet add package Josupeit.Mail.Milter
```

Targets `net8.0`. Speaks **wire version 6** of the protocol and negotiates down to whatever the MTA offers, so an older Postfix or Sendmail still works and simply gets less.

## Writing a filter

A filter is a `partial` class implementing `IMilterContext`. Every callback has a default implementation that answers "continue", so you override only the events you actually want.

```csharp
using Josupeit.Mail.Milter;
using Action = Josupeit.Mail.Milter.Action;

internal sealed partial class RejectBigSenders : IMilterContext
{
    public ValueTask<CommitAction> OnSender(SenderInformation info, CancellationToken cancellationToken)
    {
        // AddressString is the raw envelope argument, angle brackets and all: "<user@example.com>".
        return info.AddressString.EndsWith("@spam.example.com>", StringComparison.OrdinalIgnoreCase)
            ? new(CommitActions.Reject)
            : new(CommitActions.Continue);
    }

    public IAsyncEnumerable<Action> OnEndOfBody()
    {
        var actions = new Action[]
        {
            ModificationActions.AddHeader("X-Filtered-By", "RejectBigSenders"),
            CommitActions.Accept,
        };

        return actions.ToAsyncEnumerable();
    }
}
```

Then host it on a socket:

```csharp
using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

socket.Bind(new IPEndPoint(IPAddress.Loopback, 8894));
socket.Listen();

var milter = new Milter<RejectBigSenders>(socket);

milter.Start();

// ... run until shutdown ...

// Stop waits for the sessions still in flight, so a message being filtered right now is not cut off.
await milter.Stop();
```

and point the MTA at it - for Postfix, in `main.cf`:

```
smtpd_milters = inet:127.0.0.1:8894
```

Pass a factory when the filter needs dependencies:

```csharp
var milter = new Milter<RejectBigSenders>(socket, () => new RejectBigSenders(logger));
```

Without one the context comes from `Activator.CreateInstance`, which requires a public parameterless constructor.

## Callbacks

| Callback                  | Event                                                 | Returns                    |
| ------------------------- | ----------------------------------------------------- | -------------------------- |
| `OnConnectionEstablished` | A client connected; carries the hostname and endpoint | `CommitAction`             |
| `OnClientHello`           | `HELO` / `EHLO`                                       | `CommitAction`             |
| `OnSender`                | `MAIL FROM`                                           | `CommitAction`             |
| `OnRecipient`             | `RCPT TO`, once per recipient                         | `CommitAction`             |
| `OnMessageDataStarted`    | `DATA`; the envelope is complete, the content is not  | `CommitAction`             |
| `OnHeader`                | One message header                                    | `CommitAction`             |
| `OnEndOfHeaders`          | The header block ended                                | `CommitAction`             |
| `OnBodyChunk`             | A slice of the body, as `ReadOnlySpan<byte>`          | `CommitAction`             |
| `OnEndOfBody`             | The message is complete                               | `IAsyncEnumerable<Action>` |
| `OnUnknownCommand`        | An SMTP verb the MTA does not recognise               | `CommitAction`             |
| `OnDefineMacro`           | The MTA supplied macro values                         | nothing                    |

The body arrives in chunks, not in one piece - Postfix caps each at 64 KiB - so a filter that needs the whole body has to accumulate it.

`OnUnknownCommand` sits at the end of the table rather than somewhere in the flow because it has no fixed place in the conversation: an unrecognised verb can arrive wherever the client sends it, including before `HELO` and after the body. It needs an MTA speaking protocol version 3 or above, and `OnMessageDataStarted` needs version 4 - against anything older they simply never fire.

**A context instance serves one message, not one connection.** Postfix keeps a connection open across several messages, and the library builds a fresh context for each, so per-message state in a field is safe and state that must outlive a message is not.

## Actions

Every callback except `OnEndOfBody` and `OnDefineMacro` returns exactly one **commit action**, built from `CommitActions`:

| Action                | Effect                                                           |
| --------------------- | ---------------------------------------------------------------- |
| `Continue`            | Carry on with the next event                                     |
| `Accept`              | Accept the message, skip the remaining callbacks                 |
| `Reject`              | Reject with a permanent error (5xx)                              |
| `Defer`               | Reject with a temporary error (4xx), so the sender retries       |
| `Discard`             | Accept on the wire, then silently drop the message               |
| `Reply(code, text)`   | Reject with a reply you choose; the code must be 4xx or 5xx      |
| `SkipRemainingEvents` | Stop being sent the rest of this repeating event; decide nothing |

`SkipRemainingEvents` is the odd one: it settles nothing about the message, it only tells the MTA to stop streaming an event the filter has finished with. Answer it from `OnBodyChunk` once a body has been judged and the remaining chunks are never delivered. Only three events repeat and so only three can be skipped - headers, recipients and body chunks; answering it anywhere else, or yielding it from `OnEndOfBody`, throws. Against an MTA that did not grant it, it degrades to `Continue` rather than failing, because losing an optimisation must not drop mail.

`OnEndOfBody` returns a sequence instead: zero or more **modification actions** from `ModificationActions`, optionally interleaved with `ProgressAction.Instance`, and then **exactly one** commit action to end it. That shape is enforced - yielding nothing, forgetting the commit action, or yielding anything after it throws `InvalidOperationException`.

| Modification                       | Effect                                                       |
| ---------------------------------- | ------------------------------------------------------------ |
| `AddHeader(name, value)`           | Append a header                                              |
| `InsertHeader(index, name, value)` | Insert a header at a chosen position                         |
| `ChangeHeader(name, value, index)` | Replace the _index_-th occurrence of a header                |
| `DeleteHeader(name, index)`        | Remove the _index_-th occurrence of a header                 |
| `AddRecipient(address)`            | Add an envelope recipient                                    |
| `AddRecipient(address, eSmtpArgs)` | Add an envelope recipient with ESMTP parameters              |
| `DeleteRecipient(address)`         | Remove an envelope recipient                                 |
| `ChangeSender(sender, eSmtpArgs)`  | Replace the envelope sender                                  |
| `ReplaceBody(stream, leaveOpen)`   | Replace the message body                                     |
| `QuarantineMessage(reason)`        | Hold the message in the MTA's queue instead of delivering it |

`InsertHeader`, the ESMTP overload of `AddRecipient` and `ChangeSender` all need an MTA speaking protocol version 6. A filter that builds one of them derives `ProtocolVersionRequirement.Version6`, and negotiation fails against an older MTA rather than letting the modification be refused silently.

`ProgressAction.Instance` decides nothing; it only tells the MTA that the filter is still working, so the reply timeout restarts. Yield it from a long-running `OnEndOfBody` to avoid being timed out.

Every action is built through those two factories, and there is no other way: the records have internal constructors and get-only properties, so neither `new` nor a `with` expression can get past the validation. Control characters are rejected in every outbound string, and header names must be RFC 5322 `ftext` - which is what stops a filter from splicing a second header, or a whole extra reply, into the message through an unsanitised value.

## Declaring requirements

The six static properties on `IMilterContext` are part of the handshake with the MTA, not decoration:

- **`ProtocolRequirements`** lists the events you want delivered. The MTA skips the rest, so narrowing this is a real saving - a filter that only looks at senders never has the body streamed to it. `EndOfBody` is deliberately `0`: it cannot be excluded.
- **`ActionRequirements`** lists the modifications you intend to make. The MTA rejects any modification it was not warned about during negotiation, so a filter that adds a header must say `AddHeaders` here.
- **`NoReplyRequirements`** lists the events the MTA need not wait for an answer to. A round trip saved per event, for every event the filter can only ever answer `Continue` to.
- **`DeliveryRequirements`** asks for recipients the MTA has already rejected to be delivered anyway, and for header values to arrive with their leading space intact.
- **`MacroRequirements`** names the macros wanted at each stage, so the MTA stops sending the ones the filter never reads.
- **`ProtocolVersionRequirement`** is the lowest wire version the filter actually needs. Negotiation fails against an MTA below it, which turns a feature silently disappearing into a loud error.

The package writes all six for you. That is why the example above declares none of them, and why the class is `partial`: a source generator reads which callbacks you overrode and which actions your code builds, and emits the properties to match. Overriding `OnSender` produces `ProtocolRequirements.Sender`; calling `ModificationActions.AddHeader` anywhere in the filter produces `ActionRequirements.AddHeaders`.

The first two are `static abstract`, so a filter that somehow loses the generator fails to compile rather than negotiating something wrong - there is no safe default for "which events" or "which modifications". The other four are `static virtual` with a default that behaves exactly as version 2 did, which makes the generator an optimiser for them rather than a load-bearing part.

It follows the actions across method and class boundaries, and it takes the union over every branch - a modification behind an `if` still has to be negotiated, whether or not that `if` is ever true. What it cannot follow is an action arriving from outside the compilation: from an interface, an abstract member, or another assembly. Rather than guess - too few makes the MTA refuse the modification, too many hands the filter rights over the message it does not need - it reports **MILT002** and leaves the property to you.

**MILT003** is the same idea for macros. The derivation reads stage-guarded constant lookups in `OnDefineMacro`; anything it cannot pin to a stage and a name - a computed key, or the very common `_macros = info.Macros` stash - is reported rather than guessed, because a list that leaves a macro out fails at runtime instead of at negotiation. The way out is one line: `public static MacroRequirements MacroRequirements => MacroRequirements.None;`, which sends no list at all and lets the MTA use its own configuration.

Declaring any of the six by hand always wins, and is the way out of MILT002 and MILT003 as well as the way to narrow what was derived:

```csharp
internal sealed partial class RejectBigSenders : IMilterContext
{
    public static ActionRequirements ActionRequirements => ActionRequirements.AddHeaders;

    // ...
}
```

## Bounding the connection

`MilterOptions` caps two things that are otherwise attacker-controlled:

```csharp
var milter = new Milter<RejectBigSenders>(socket, new MilterOptions
{
    MaxFrameLength = 1024 * 1024,   // default; upper bound on a single frame's length prefix
    MaxConcurrentSessions = 1024,   // default; connections past this are closed, not queued
});
```

## Going further

The wire layer is public and non-sealed on purpose - `LinkReader`/`LinkWriter`, `RequestParser`/`ResponseFormatter`, the packet hierarchies and their visitors - so a consumer who needs to reach below the callback surface can subclass or substitute rather than fork.

`Milter<TContext>` is unsealed too, and builds the per-connection pipeline through seven `protected virtual` factory methods - the link reader and writer, the parser and formatter, both packet factories, and the session they are handed to. Override one and the rest is inherited. `Session<TContext>` has a public constructor for the same reason, and both packet hierarchies accept a visitor of your own.

## License

[EUPL-1.2](https://joinup.ec.europa.eu/collection/eupl/eupl-text-eupl-12)
