using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class ReplayRecordingNegotiatedState(IProtocolStateMachine stateMachine) : ProtocolState
{
    private readonly List<RequestPacket> _packetReplayHistory = new(capacity: 8);

    public override IAsyncEnumerable<ResponsePacket> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnBodyChunk, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnClientHello, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessConnectionAttempt(ConnectRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _packetReplayHistory.Add(request);

        return ProcessRequestCore(request, static ca => ca.OnConnectionEstablished, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnEndOfBody, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnEndOfHeaders, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnHeader, cancellationToken);
    }

    public override async IAsyncEnumerable<ResponsePacket> ProcessMacroDefinition(DefineMacroRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _packetReplayHistory.Add(request);

        var contextAdapter = stateMachine.EnsureContextAdapter();

        await foreach (var response in contextAdapter.OnDefineMacro(request, cancellationToken).ConfigureAwait(false))
            yield return response;
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
        return ProcessRequest(request, static ca => ca.OnRecipient, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnSender, cancellationToken);
    }

    private IAsyncEnumerable<ResponsePacket> ProcessRequest<TRequest>(TRequest request, Func<ContextAdapter, Func<TRequest, CancellationToken, IAsyncEnumerable<ResponsePacket>>> processor, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return ProcessRequestCore(request, processor, cancellationToken);
    }

    private async IAsyncEnumerable<ResponsePacket> ProcessRequestCore<TRequest>(TRequest request, Func<ContextAdapter, Func<TRequest, CancellationToken, IAsyncEnumerable<ResponsePacket>>> processor, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var contextAdapter = stateMachine.EnsureContextAdapter();

        try
        {
            await foreach (var response in processor(contextAdapter).Invoke(request, cancellationToken).ConfigureAwait(false))
                yield return response;
        }
        finally
        {
            var connectedState = new ReplayingConnectedState(stateMachine, _packetReplayHistory);

            await stateMachine.TransitionTo(connectedState).ConfigureAwait(false);
        }
    }
}