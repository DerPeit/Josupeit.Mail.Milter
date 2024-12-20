using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class ReplayingConnectedState(IProtocolStateMachine stateMachine, IReadOnlyList<RequestPacket> packetReplayHistory) : ProtocolState
{
    public override async IAsyncEnumerable<ResponsePacket> ProcessAbortion(AbortRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            yield break;
        }
        finally
        {
            await stateMachine.DisposeContextAdapter().ConfigureAwait(false);
            
            var abortedState = new ReplayingAbortedState(stateMachine, packetReplayHistory);
            
            await stateMachine.TransitionTo(abortedState).ConfigureAwait(false);
        }
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnBodyChunk, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnClientHello, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnEndOfBody, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnEndOfHeaders, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnHeader, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnDefineMacro, cancellationToken);
    }

    public override async IAsyncEnumerable<ResponsePacket> ProcessQuitting(QuitRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        await stateMachine.DisposeContextAdapter().ConfigureAwait(false);
        await stateMachine.TransitionTo(new DisconnectedState()).ConfigureAwait(false);

        yield break;
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessRecipient(RecipientRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnRecipient, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnSender, cancellationToken);
    }

    private async IAsyncEnumerable<ResponsePacket> ProcessRequest<TRequest>(TRequest request, Func<ContextAdapter, Func<TRequest, CancellationToken, IAsyncEnumerable<ResponsePacket>>> processor, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var contextAdapter = stateMachine.EnsureContextAdapter();

        await foreach (var response in processor(contextAdapter).Invoke(request, cancellationToken).ConfigureAwait(false))
            yield return response;
    }
}