// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter;
using Action = Josupeit.Mail.Milter.Action;

// Native AOT smoke test. Each block invokes the library's primary public API so the AOT compiler has
// to compile the path natively; assertions prove the path actually ran rather than silently no-op'd.
// Reaching "AOT SMOKE OK" under a native binary is the gate. Any failure returns a non-zero exit code.
try
{
    // Commit actions: the cached singletons and the parameterised ones.
    Check(CommitActions.Continue is not null, "continue");
    Check(CommitActions.Accept is not null, "accept");
    Check(CommitActions.Reject is not null, "reject");
    Check(CommitActions.Discard is not null, "discard");
    Check(CommitActions.Defer is not null, "defer");
    Check(CommitActions.Reply(450, "try again later") is not null, "reply");

    // Modification actions: every factory that the negotiation can declare.
    Check(ModificationActions.AddHeader("X-Smoke", "aot") is not null, "add header");
    Check(ModificationActions.InsertHeader(0, "X-Smoke", "aot") is not null, "insert header");
    Check(ModificationActions.ChangeHeader("X-Smoke", "aot") is not null, "change header");
    Check(ModificationActions.DeleteHeader("X-Smoke") is not null, "delete header");
    Check(ModificationActions.AddRecipient("barnacle@example.com") is not null, "add recipient v2");
    Check(ModificationActions.AddRecipient("barnacle@example.com", "NOTIFY=NEVER") is not null, "add recipient v6");
    Check(ModificationActions.DeleteRecipient("barnacle@example.com") is not null, "delete recipient");
    Check(ModificationActions.ChangeSender("<foghorn@example.com>") is not null, "change sender");
    Check(ModificationActions.QuarantineMessage("suspicious kelp") is not null, "quarantine");
    Check(ProgressAction.Instance is not null, "progress");

    // Milter construction through the factory path (the AOT-safe one).
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    socket.Listen(1);

    var milter = new Milter<SmokeContext>(socket, () => new SmokeContext());

    Check(milter is not null, "milter factory construction");

    var milterWithOptions = new Milter<SmokeContext>(socket, () => new SmokeContext(), new MilterOptions());

    Check(milterWithOptions is not null, "milter factory+options construction");

    // The context the generator derived requirements for. The static properties prove the generator
    // ran and the AOT compiler compiled the generated code.
    Check(SmokeContext.ProtocolRequirements != default, "generated protocol requirements");
    Check(SmokeContext.ActionRequirements != default, "generated action requirements");

    Console.WriteLine("AOT SMOKE OK");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"AOT SMOKE FAILED: {ex}");

    return 1;
}

static void Check(bool condition, string what)
{
    if (!condition)
        throw new InvalidOperationException($"Smoke assertion failed: {what}");
}

// A minimal context that overrides enough to exercise the generator's derivation of both
// requirement properties. Sealed so the no-reply analysis can prove something.
sealed partial class SmokeContext : IMilterContext
{
    public ValueTask<CommitAction> OnSender(SenderInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public async IAsyncEnumerable<Action> OnEndOfBody()
    {
        yield return ModificationActions.AddHeader("X-Smoke", "verified");
        yield return CommitActions.Accept;
    }
}
