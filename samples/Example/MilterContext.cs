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

// None of the six requirement properties is declared here. The source generator writes all of them:
//
//   ProtocolRequirements       from the callbacks below that are not the interface default
//   ActionRequirements         from the actions this code builds - including the one QuarantineReasons
//                              builds on its behalf, and the one behind an if, since a modification the
//                              filter might perform still has to be negotiated
//   NoReplyRequirements        from proving OnConnectionEstablished, OnClientHello and OnRecipient can
//                              only ever continue - counting a recipient is not deciding anything - so
//                              the MTA stops waiting for answers that never differ
//   DeliveryRequirements       from reading info.IsRejectedByMta below
//   MacroRequirements          from the stage-guarded macro lookup in OnDefineMacro
//   ProtocolVersionRequirement folded out of all of the above - version 6 here, and several times over:
//                              the macro list, the skip, the rejected recipients and the no-reply flags
//                              each need it on their own
//
// The class is sealed on purpose: an overridable callback proves nothing about what will actually run,
// so no-reply is never derived for a context that a derived type could still change.
internal sealed partial class MilterContext : IMilterContext
{
    private static readonly byte[] _marker = Encoding.ASCII.GetBytes("free cruise");

    private bool _suspicious;
    private string? _queueId;
    private int _refusedRecipients;

    public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
    {
        if (info.ProtocolStage == ProtocolStage.EndOfBody)
            _queueId = info.Macros["i"];

        return new();
    }

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

    // Reading IsRejectedByMta is what makes the generator ask the MTA for recipients it has already
    // refused. A filter that never looks never gets them.
    public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken)
    {
        if (info.IsRejectedByMta)
            _refusedRecipients++;

        return new(CommitActions.Continue);
    }

    public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
    {
        _suspicious |= chunk.IndexOf(_marker) >= 0;

        // Once the body has given itself away there is nothing left to learn from the rest of it, so the
        // MTA is told to stop sending chunks. Against an MTA that will not grant it this is a Continue
        // and the remaining chunks simply arrive.
        return new(_suspicious ? CommitActions.SkipRemainingEvents : CommitActions.Continue);
    }

    public IAsyncEnumerable<Action> OnEndOfBody()
    {
        var actions = new List<Action> { ModificationActions.AddHeader("X-Filtered-By", "Josupeit.Mail.Milter") };

        if (_queueId != null)
            actions.Add(ModificationActions.InsertHeader(index: 0, "X-Filter-Queue-Id", _queueId));

        if (_refusedRecipients > 0)
            actions.Add(ModificationActions.AddHeader("X-Refused-Recipients", _refusedRecipients.ToString()));

        if (_suspicious)
            actions.Add(QuarantineReasons.ForSuspiciousBody());

        actions.Add(CommitActions.Continue);

        return actions.ToAsyncEnumerable();
    }
}
