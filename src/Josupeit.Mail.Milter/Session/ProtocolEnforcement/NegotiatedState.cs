// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class NegotiatedState(IProtocolStateMachine stateMachine, ProtocolNegotiationOutcome negotiation) : ProtocolState
{
    // An MTA is in a transaction from the moment it begins a connection event, which is before it decides
    // whether to deliver one to us at all. So a filter that requires neither Connect nor ClientHello sits
    // in this state and still receives an abortion - Postfix sends one on RSET, on a repeated EHLO and on
    // STARTTLS. Nothing message-specific has reached the context here yet, so resetting to before HELO is
    // just dropping it; the state itself already is that point.
    public override async ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await stateMachine.DisposeContextAdapter().ConfigureAwait(false);

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnBodyChunk(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnClientHello(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessConnectionAttempt(ConnectRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnConnectionEstablished(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnEndOfBody(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnEndOfHeaders(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnHeader(packet));
    }

    // A macro definition carries no message content, so it neither starts the next message nor transitions.
    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contextAdapter = stateMachine.EnsureContextAdapter(negotiation);

        return new(contextAdapter.OnDefineMacro(request));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMessageDataStart(MessageDataRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnMessageDataStarted(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessQuitting(QuitRequestPacket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(AsyncEnumerable.Empty<ResponsePacket>().Finally(ToDisconnectedState));

        async Task ToDisconnectedState()
        {
            await stateMachine.DisposeContextAdapter().ConfigureAwait(false);
            await stateMachine.TransitionTo(new DisconnectedState()).ConfigureAwait(false);
        }
    }

    // The SMTP connection is over but the milter connection is not: a different client follows on this
    // socket. The context goes, because the message it was serving is over, and the machine returns to
    // the state that precedes a connection rather than the one that follows it - ConnectedState does not
    // accept SMFIC_CONNECT, so landing there would make the next client's first packet a violation.
    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessQuittingForNewConnection(QuitForNewConnectionRequestPacket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(AsyncEnumerable.Empty<ResponsePacket>().Finally(ToNegotiatedState));

        async Task ToNegotiatedState()
        {
            await stateMachine.DisposeContextAdapter().ConfigureAwait(false);
            await stateMachine.TransitionTo(NegotiatedStateFactory.Create(stateMachine, stateMachine.Configuration, negotiation)).ConfigureAwait(false);
        }
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRecipient(RecipientRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnRecipient(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnSender(packet));
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessUnknownCommand(UnknownCommandRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnUnknownCommand(packet));
    }

    // The callback arrives as (adapter, packet) rather than as a method group off the adapter. A method
    // group with a receiver cannot be cached, so the shorter `static ca => ca.OnHeader` allocated a
    // delegate for every event that reached this state, and events are the hot path here.
    private ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRequest<TRequest>(TRequest request, Func<ContextAdapter, TRequest, IAsyncEnumerable<ResponsePacket>> processor)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var contextAdapter = stateMachine.EnsureContextAdapter(negotiation);
        var responses = processor(contextAdapter, request);

        return new(responses.Finally(ToConnectedState));

        async Task ToConnectedState() => await stateMachine.TransitionTo(new ConnectedState(stateMachine, negotiation)).ConfigureAwait(false);
    }
}
