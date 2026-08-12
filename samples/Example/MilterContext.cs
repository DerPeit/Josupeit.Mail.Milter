// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter;
using Action = Josupeit.Mail.Milter.Action;

namespace Example;

internal partial class MilterContext : IMilterContext
{
    public static ActionRequirements ActionRequirements => ActionRequirements.All;

    public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
    {
        return new();
    }

    public ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    ValueTask<CommitAction> IMilterCallbacks.OnClientHello(ClientHelloInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    //public ValueTask<CommitAction> OnSender(SenderRecipientInformation info, CancellationToken cancellationToken)
    //{
    //    return new(CommitActions.Continue);
    //}

    //public ValueTask<CommitAction> OnRecipient(SenderRecipientInformation info, CancellationToken cancellationToken)
    //{
    //    return new(CommitActions.Continue);
    //}

    public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    //public ValueTask<CommitAction> OnEndOfHeaders(CancellationToken cancellationToken)
    //{
    //    return new(CommitActions.Continue);
    //}

    public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public IAsyncEnumerable<Action> OnEndOfBody()
    {
        return new[] { CommitActions.Continue }.ToAsyncEnumerable();
    }
}