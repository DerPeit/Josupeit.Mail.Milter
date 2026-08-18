// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter;
using Action = Josupeit.Mail.Milter.Action;

namespace Example;

// Neither ProtocolRequirements nor ActionRequirements is declared here. The source generator writes
// both: the first from the callbacks below that are not the interface default, the second from the
// actions this code builds - including the one QuarantineReasons builds on its behalf, and including
// the one behind an if, since a modification the filter might perform still has to be negotiated.
internal partial class MilterContext : IMilterContext
{
    private static readonly byte[] _marker = Encoding.ASCII.GetBytes("free cruise");

    private bool _suspicious;

    public ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
    {
        Console.WriteLine($"connection from {info.Hostname}");

        return new(CommitActions.Continue);
    }

    ValueTask<CommitAction> IMilterCallbacks.OnClientHello(ClientHelloInformation info, CancellationToken cancellationToken)
    {
        Console.WriteLine($"greeted as {info.Hostname}");

        return new(CommitActions.Continue);
    }

    public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
    {
        _suspicious |= chunk.IndexOf(_marker) >= 0;

        return new(CommitActions.Continue);
    }

    public IAsyncEnumerable<Action> OnEndOfBody()
    {
        var actions = new List<Action> { ModificationActions.AddHeader("X-Filtered-By", "Josupeit.Mail.Milter") };

        if (_suspicious)
            actions.Add(QuarantineReasons.ForSuspiciousBody());

        actions.Add(CommitActions.Continue);

        return actions.ToAsyncEnumerable();
    }
}
