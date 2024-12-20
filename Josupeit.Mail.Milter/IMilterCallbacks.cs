using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Generator;

namespace Josupeit.Mail.Milter;

public interface IMilterCallbacks
{
    ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
    {
        return new();
    }

    [MilterCallbackMarker(nameof(ProtocolRequirements.Connect))]
    ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    [MilterCallbackMarker(nameof(ProtocolRequirements.ClientHello))]
    ValueTask<CommitAction> OnClientHello(ClientHelloInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    [MilterCallbackMarker(nameof(ProtocolRequirements.Sender))]
    ValueTask<CommitAction> OnSender(SenderRecipientInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    [MilterCallbackMarker(nameof(ProtocolRequirements.Recipient))]
    ValueTask<CommitAction> OnRecipient(SenderRecipientInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    [MilterCallbackMarker(nameof(ProtocolRequirements.Header))]
    ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
    {
        return new (CommitActions.Continue);
    }

    [MilterCallbackMarker(nameof(ProtocolRequirements.EndOfHeaders))]
    ValueTask<CommitAction> OnEndOfHeaders(CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    [MilterCallbackMarker(nameof(ProtocolRequirements.Body))]
    ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    IAsyncEnumerable<Action> OnEndOfBody()
    {
        return new Action[] { CommitActions.Continue }.ToAsyncEnumerable();
    }
}