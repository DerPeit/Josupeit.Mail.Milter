# Milter review - fix decisions

Four background review agents (security, code quality, protocol conformance, test coverage) reviewed the
whole codebase on 2026-08-10. ~50 findings, all now decided one at a time. **Nothing implemented yet.**

Cross-confirmation was high: the quarantine-reason drop was found independently by all four agents;
the ABORT-in-NegotiatedState bug and the frame-length underflow by three each.

## Library fixes

| # | Finding | Decision |
|---|---------|----------|
| 1 | RequestParser.cs:70 `length - 1` uint underflow + no upper bound | **Configurable MaxFrameLength** on `Milter<T>`, 64KiB default. Guard `<1` and `>max` before the subtraction; assert the same bound inside `LinkReader.ReadData` |
| 2 | Session.cs:76 bare `await responseFrame`, no timeout | **`internal ValueTask WaitAsync(TimeSpan, TimeProvider, CancellationToken)` on ResponseFrame**; the TaskCompletionSource stays private |
| 3 | ABORT throws in NegotiatedState / ReplayRecordingNegotiatedState | **Override `ProcessAbortion` in both**, mirroring ConnectedState |
| 4 | ResponseFormatter.cs:26 missing `.Unwrap()`; response channel never completed | **Unwrap + complete the channel in Session.Run's finally + hardening** (already-running guard, StopAsync null guard) |
| 5 | ReplayRecordingNegotiatedState.cs:12,49 unbounded macro history | **Cap count and total bytes**, fail the session past the cap |
| 6 | RequestPacketFactory.cs:126 `macros.Add` throws on duplicate name | **Last definition wins** -> `macros[name] = value` |
| 7 | RequestPacketFactory.cs:192 `new Version((int)version, 0)` overflow | **Range-check**, route into the existing unsupported-version path |
| 8 | ResponseFrameFactory.cs:230 quarantine reason dropped | **Encode Reason + NUL** |
| 9 | No CR/LF/NUL validation on outbound strings | **Validate in the factories**: reject C0 controls; header names must be RFC 5322 ftext |
| 10 | ReplyAction `%` not doubled (Postfix un-doubles at milter8.c:1305) | **Double at serialization** in ResponseFrameFactory |
| 11 | Header index 0-based in API, 1-based on the wire | **Keep the 0-based API, emit `index + 1`** at the boundary |
| 12 | ContextAdapter.cs:137 EOB action sequence unenforced | **Enforce and throw** - approved code below |
| 13 | ReplayingAbortedState.cs:107 illegal reply to a macro packet | **`responseExpected` flag through ProcessRequest**; throw instead of yielding |
| 14 | Milter.cs:94 accept loop has no exception handling | **Postpone** - explanatory comment only |
| 15 | Milter.cs:92 no concurrent session cap | **SemaphoreSlim gate**, configurable `MaxConcurrentSessions` |
| 20 | Encoding: Latin-1 in vs ASCII out | **Latin-1 both directions** |
| 21 | ReplaceBodyAction.cs:23 chunks can exceed CHUNK_SIZE | **Slice to `memoryOwner.Memory[..CHUNK_SIZE]`** |
| 22 | Session.cs:57 pooled buffer leaks if the factory throws | **try/catch around the factory call**, dispose and rethrow |
| 23 | PipeReader per packet + byte-by-byte StringBuilder decode | **Rewrite on spans** - `IndexOf((byte)0)` + `Encoding.Latin1.GetString`. Deletes SequenceExtensions.AsSpanInvoke and FuncOfSpan |
| 25 | LinkWriter.cs:21,41 pooled rent for 4-byte and 1-byte writes | **stackalloc**, keep the two methods separate |
| 26 | TaskCreationOptions.LongRunning in three places | **Task.Run for RequestParser + ResponseFormatter**; leave Milter's accept loop |
| 27 | `Milter<TContext>` needs a parameterless ctor, no `new()` constraint | **Make the contextFactory overload public**; keep Activator.CreateInstance as fallback |
| 29 | ReplaceBodyAction disposes the caller's Stream | **Add a `leaveOpen` flag**, default false |
| 31 | Action records have public ctors, bypassing factory validation | **Make the constructors internal** |
| 32 | Session.cs:13 vs :47 config built twice; Configuration unread | **Use the field, drop the interface member** |
| 33 | SMFIR_PROGRESS defined but unreachable | **Add a ProgressAction** - non-commit, yieldable from OnEndOfBody |
| 34 | Null checks deferred inside async iterators | **Split the eight async iterators into wrapper + Core.** ProtocolState.cs:14-132 and both ProcessAbortion overloads are NOT iterators and are correct - leave them |
| 35 | ContextAdapter.cs:55-135 eight near-identical bodies | **Generic `Invoke<TInfo>` helper** - approved code below |
| 36 | Cosmetic sweep | **All of it** - slice chains to a running offset, blank lines, trailing whitespace, drop `in` on the PipeReader param, fix the two wrong doc comments |
| 37 | CONNECT address modelling | **Replace `AddressFamily` + `int Port` + `IPAddress` with `EndPoint? EndPoint`** in BOTH ConnectRequestPacket and ConnectionInformation. IPEndPoint or UnixDomainSocketEndPoint. Nullable because SMFIA_UNKNOWN carries no address - **comment the reason**. No separate address-text field |
| 38 | UnixDomainSocketEndPoint throws outside 1-108 chars | **Let it bubble up; add a comment.** Accepted as a session kill |
| 39 | `Async` suffix on the three StopAsync methods | **Rename to Stop** |

## Docs

| # | Item | Decision |
|---|------|----------|
| 16 | Roslyn ships in consumer output (csproj:25) | **Skip, document** - the generator may be deleted; worth undecided |
| 17 | ~20 wire-format types public against CONTRIBUTING.md:21 | **Leave public, rewrite the doc** - library code, consumers are meant to derive and override; some types are deliberately public, non-sealed, with protected/virtual members |
| 18 | Milter.cs:164 `catch { }`, no logging anywhere | **Skip** - comment only |
| 19 | AbortedState:46 vs ReplayingAbortedState:47 macro-transition drift | **Document as intentional** - the replay forces the transition |
| 40 | Example/Program.cs is still the Hello World template | **Make it a real example** - bind a socket, construct `Milter<MilterContext>`, start it; disable ImplicitUsings |
| 42 | CLAUDE.md says one context per session; it is per message | **Fix the docs** |

## Skipped

| # | Item | Reason |
|---|------|--------|
| 24 | `.AsTask().WaitAsync(...)` per packet | Correctness first; current model reads well |
| 28 | OnEndOfBody has no CancellationToken | ProtocolState forwarders are correct via EnumeratorCancellation substitution; only the ~10 non-iterator actions have dead checks and they are harmless |
| 30 | ~12 allocations per SMTP event | Revisit if profiling shows it matters |
| 41 | Generator G1-G5 | May be deleted entirely - do not invest |
| 49 | Test with a plain hand-written IMilterContext | - |

## Tests

| # | Item | Decision |
|---|------|----------|
| 43 | Integration-only suite | **New `Josupeit.Mail.Milter.UnitTests` project; rename the existing one to `Josupeit.Mail.Milter.IntegrationTests`.** Four surfaces: ResponseFrameFactory byte-exact (all 13 visitors), RequestParser/LinkReader malformed input over an in-memory Pipe, ProtocolState transition matrix, RequestPacketFactory branches. **Update InternalsVisibleTo in Josupeit.Mail.Milter.csproj for both names** |
| 44 | Defer test passes even if the milter crashes | **Assert on the milter side** that the relevant callback ran; leave main.cf alone |
| 45 | Fixed ports + TearDown cross-test race | **Fix the race only** - await outstanding sessions before clearing the resolver. Leave the fixed ports |
| 46 | All seven modification actions have zero coverage | **All seven, integration, asserting the delivered mail** |
| 47 | AbortedState / ReplayingAbortedState never entered | **Both levels** - integration RSET mid-transaction, plus unit tests for replay ordering, the responseExpected guard and second-context-after-dispose |
| 48 | Connection reuse across messages never produced | **Integration test with two messages over one SMTP connection** |
| 50 | MilterTests.cs is 202KB, ~80% one Lorem literal | **Embedded resource** |

Also: four existing connection tests need rework from #37 alone - `MilterTests.cs:89` (IPv4), `:114`
(unknown family), `:142` (unavailable address), `:162` (local submission).

## Approved code

### #12 - ContextAdapter.OnEndOfBody

```csharp
public IAsyncEnumerable<ResponsePacket> OnEndOfBody(
    EndOfBodyRequestPacket request, CancellationToken cancellationToken)
{
    var context = _context ??= contextFactory();

    return OnEndOfBodyCore(context.OnEndOfBody(), cancellationToken);
}

private static async IAsyncEnumerable<ResponsePacket> OnEndOfBodyCore(
    IAsyncEnumerable<Action> actions,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var committed = false;

    await foreach (var action in actions
        .WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        if (committed)
            throw new InvalidOperationException(
                "OnEndOfBody yielded an action after the commit action.");

        committed = action is CommitAction;

        await foreach (var response in action.CreateResponsePackets()
            .WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return response;
    }

    if (!committed)
        throw new InvalidOperationException(
            "OnEndOfBody must yield exactly one commit action.");
}
```

### #35 - ContextAdapter.Invoke

```csharp
private async IAsyncEnumerable<ResponsePacket> Invoke<TInfo>(
    TInfo info,
    Func<IMilterContext, TInfo, CancellationToken, ValueTask<CommitAction>> callback,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var context = _context ??= contextFactory();

    var commitAction = await callback(context, info, cancellationToken)
        .ConfigureAwait(false);

    await foreach (var response in commitAction.CreateResponsePackets()
        .WithCancellation(cancellationToken).ConfigureAwait(false))
        yield return response;
}

public IAsyncEnumerable<ResponsePacket> OnConnectionEstablished(
    ConnectRequestPacket request, CancellationToken cancellationToken)
{
    return Invoke(
        new ConnectionInformation(request.HostName, request.EndPoint),
        static (ctx, info, ct) => ctx.OnConnectionEstablished(info, ct),
        cancellationToken);
}
```

## Verified facts behind #37/#38 (net8.0, Windows)

- `UnixDomainSocketEndPoint` constructs fine and reports `AddressFamily.Unix`
- Both `IPEndPoint` and `UnixDomainSocketEndPoint` have **value equality**, so `ConnectionInformation`
  stays sound as a `readonly record struct`
- Path length limit is **1-108 characters**; empty and oversized both throw `ArgumentOutOfRangeException`
- `IPAddress.None` is `255.255.255.255`, not `0.0.0.0`
- `EndPoint.AddressFamily` covers what the removed `Family` property provided

## Added during implementation

| # | Item | Decision |
|---|------|----------|
| 51 | The CancellationToken on IProtocolState was meaningless: the interface declared none, twelve explicit implementations passed `CancellationToken.None`, and the real token was substituted into the deferred iterators by whoever enumerated them | **Redesigned.** States take the token on the interface and return `ValueTask<IAsyncEnumerable<ResponsePacket>>`. The forwarders and every `[EnumeratorCancellation]` in the states are gone; eager work is eager and awaits properly. Transitions still run after enumeration, via Ix's `Finally`. Added `System.Interactive.Async` `[6.0.1,7)` |
| 52 | #33 gave progress its own family: it is neither a commit nor a modification, so neither factory class fits | **`ProgressAction.Instance` stays the entry point.** A third factory holding one member, and folding it into `CommitActions` where it commits nothing, were both weighed and rejected |
| 53 | #31 could only close the constructor by dropping the positional records | **Explicit internal constructors and get-only properties.** Positional records force a public constructor and `init` setters, so `with` was a second way past the factory validation. Both are closed; `Deconstruct` is gone with them |
| 54 | #35 left the seven adapter entry points taking a `CancellationToken` they only forwarded into a deferred iterator, which the enumerator then replaced. `Session.Run` already enumerates with `WithCancellation`, so a non-default argument had Roslyn build a linked `CancellationTokenSource` per packet to link the session token to itself | **Dropped the parameter.** The token lives only on the `[EnumeratorCancellation]` parameters, defaulted - the shape #51 established. The states lose it from their `ProcessRequest` helpers; `ReplayingAbortedState` keeps its own because it awaits the replay eagerly |
| 55 | #39 was never applied - `IRequestParser.StopAsync` and both implementations still carried the suffix. `Milter.Stop` was always `Stop`, so those three were the whole finding | **Renamed.** Found while working through Area F |

## Measured during Area H

- **The quarantine reason is unobservable through Postfix.** #8 was found by all four reviewers, but
  Postfix reads the reason off SMFIR_QUARANTINE, logs its own fixed `milter triggers HOLD action` text, and
  holds the message just as readily when the reason is absent - verified by reverting the fix. The fix is
  still right (libmilter sends reason + NUL, and another MTA may surface it), but only the byte-exact unit
  test can prove it.
- **The header index only shows up on the second occurrence.** Postfix treats the zero a naive
  implementation sends as a one, so a single-occurrence change works with or without #11. The
  second-occurrence test fails without the fix; the simple one does not.
- **A macro packet on its own builds a context.** `ProcessMacroDefinition` calls `EnsureContextAdapter`, so
  Postfix's per-command macros create contexts that receive nothing but `OnDefineMacro` and are then
  disposed. An aborted-then-resent conversation created four contexts where two would do. Not a
  correctness problem - the callbacks all land on the right context - but the user's constructor runs more
  often than the "one context per message" model suggests. **No decision taken.**

## Follow-up work

- ~~Audit every `Task` and `ValueTask` in the library~~ **Done.** Measured: `ValueTask<T>` saves 72 bytes per
  call whenever the result is known synchronously and is never worse, so every `ValueTask<T>` stays.
  Non-generic `ValueTask` is 0 vs 0 against `Task`, so `Milter.Stop`, `ISession.Run`/`Session.Run`,
  `IRequestParser.StopAsync` with both implementations, and `ResponseFrame.WaitAsync` became `Task`.
  `ILinkWriter` and `DisposeContextAdapter` keep `ValueTask` - the first as a pooling contract mirroring
  `PipeWriter`, the second because it forwards `IAsyncDisposable.DisposeAsync()`. `OnDefineMacro` keeps
  `ValueTask` for consistency across the eight callbacks. The rule is now in CONTRIBUTING.md.
- `IStateMachine.TransitionTo` is provably synchronous (`_protocolState = state; return new();`) yet
  async-shaped, which is what forces `Finally` onto its `Func<Task>` overload. Making it `void` would let
  about half the state transitions become plain `Action` locals. Considered and **not** taken.
- Argument validation on the three `async` state overrides
  (`NegotiatedState.ProcessAbortion`, `ReplayRecordingNegotiatedState.ProcessAbortion`,
  `ReplayingAbortedState.ProcessRequest`) is now reported when the returned task is awaited rather than at
  the call. Callers await immediately, so it surfaces at the same place - but it is weaker than what #34
  established. A non-async wrapper around a local function would restore the synchronous throw.

## Open note

`default(ResponseFrame)` leaves `_taskCompletionSource` null - a struct's implicit parameterless ctor skips
field initializers, so awaiting one would NRE. Not currently reachable; no decision taken.
