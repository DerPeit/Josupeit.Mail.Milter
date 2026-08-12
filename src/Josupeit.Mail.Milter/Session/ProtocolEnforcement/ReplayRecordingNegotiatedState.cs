// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class ReplayRecordingNegotiatedState(IProtocolStateMachine stateMachine) : ProtocolState
{
    private readonly List<RequestPacket> _packetReplayHistory = new(capacity: 8);

    // See NegotiatedState for why an abortion reaches this state at all. The recorded packets are kept:
    // they describe the connection, which outlives the message that was aborted and still has to be
    // replayed into the context that serves the next one.
    public override async ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        await stateMachine.DisposeContextAdapter().ConfigureAwait(false);

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnBodyChunk);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnClientHello);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessConnectionAttempt(ConnectRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _packetReplayHistory.Add(request);

        return ProcessRequestCore(request, static ca => ca.OnConnectionEstablished);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnEndOfBody);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnEndOfHeaders);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnHeader);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _packetReplayHistory.Add(request);

        var contextAdapter = stateMachine.EnsureContextAdapter();

        return new(contextAdapter.OnDefineMacro(request));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessQuitting(QuitRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return new(AsyncEnumerable.Empty<ResponsePacket>().Finally(ToDisconnectedState));

        async Task ToDisconnectedState()
        {
            await stateMachine.DisposeContextAdapter().ConfigureAwait(false);
            await stateMachine.TransitionTo(new DisconnectedState()).ConfigureAwait(false);
        }
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRecipient(RecipientRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnRecipient);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnSender);
    }

    private ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRequest<TRequest>(TRequest request, Func<ContextAdapter, Func<TRequest, IAsyncEnumerable<ResponsePacket>>> processor)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return ProcessRequestCore(request, processor);
    }

    private ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRequestCore<TRequest>(TRequest request, Func<ContextAdapter, Func<TRequest, IAsyncEnumerable<ResponsePacket>>> processor)
    {
        var contextAdapter = stateMachine.EnsureContextAdapter();
        var responses = processor(contextAdapter).Invoke(request);

        return new(responses.Finally(ToReplayingConnectedState));

        async Task ToReplayingConnectedState() => await stateMachine.TransitionTo(new ReplayingConnectedState(stateMachine, _packetReplayHistory)).ConfigureAwait(false);
    }
}
