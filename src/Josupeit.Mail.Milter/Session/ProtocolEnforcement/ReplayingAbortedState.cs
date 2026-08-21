// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class ReplayingAbortedState(IProtocolStateMachine stateMachine, ProtocolNegotiationOutcome negotiation, IReadOnlyList<RequestPacket> packetReplayHistory) : ProtocolState
{
    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(AsyncEnumerable.Empty<ResponsePacket>());
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnBodyChunk(packet), negotiation.IsAnsweringBody, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnClientHello(packet), negotiation.IsAnsweringClientHello, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        // End-of-body is the one event with no no-reply flag in the protocol: it carries the verdict,
        // so the MTA always waits for it.
        return ProcessRequest(request, static (ca, packet) => ca.OnEndOfBody(packet), responseExpected: true, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnEndOfHeaders(packet), negotiation.IsAnsweringEndOfHeaders, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnHeader(packet), negotiation.IsAnsweringHeaders, cancellationToken);
    }

    // Unlike AbortedState, a macro definition does transition here, and that difference is deliberate:
    // replaying is what moves this state on, and it has to happen before the next message's events reach
    // the fresh context. The macro cannot be handled without it.
    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnDefineMacro(packet), responseExpected: false, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMessageDataStart(MessageDataRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnMessageDataStarted(packet), negotiation.IsAnsweringMessageData, cancellationToken);
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
        return ProcessRequest(request, static (ca, packet) => ca.OnRecipient(packet), negotiation.IsAnsweringRecipients, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnSender(packet), negotiation.IsAnsweringSender, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessUnknownCommand(UnknownCommandRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static (ca, packet) => ca.OnUnknownCommand(packet), negotiation.IsAnsweringUnknownCommands, cancellationToken);
    }

    // The callback arrives as (adapter, packet) rather than as a method group off the adapter. A method
    // group with a receiver cannot be cached, so the shorter `static ca => ca.OnHeader` allocated a
    // delegate for every event that reached this state, and events are the hot path here.
    private async ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRequest<TRequest>(TRequest request, Func<ContextAdapter, TRequest, IAsyncEnumerable<ResponsePacket>> processor, bool responseExpected, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var contextAdapter = stateMachine.EnsureContextAdapter(negotiation);

        var replayed = await Replay(contextAdapter, responseExpected, cancellationToken).ConfigureAwait(false);
        var responses = replayed ?? processor(contextAdapter, request);

        return responses.Finally(ToReplayingConnectedState);

        async Task ToReplayingConnectedState() => await stateMachine.TransitionTo(new ReplayingConnectedState(stateMachine, negotiation, packetReplayHistory)).ConfigureAwait(false);
    }

    // Replays the recorded connection packets into the fresh context, returning the decision that ends
    // processing early, or null when everything replayed asked to continue.
    //
    // An abortion "resets internal state of milter program to before SMFIC_HELO, but keeps the connection
    // open", so the connection the next message arrives on has to be described to the context again. Only
    // the connection request and macros can be in the history, by construction of the states that record it.
    private async ValueTask<IAsyncEnumerable<ResponsePacket>?> Replay(ContextAdapter contextAdapter, bool responseExpected, CancellationToken cancellationToken)
    {
        foreach (var packet in packetReplayHistory)
        {
            var replayed = packet switch
            {
                DefineMacroRequestPacket defineMacroRequestPacket => contextAdapter.OnDefineMacro(defineMacroRequestPacket),
                ConnectRequestPacket connectRequestPacket => contextAdapter.OnConnectionEstablished(connectRequestPacket),
                _ => null!,
            };

            Debug.Assert(replayed != null, "Packet replay history contained unexpected packet.");

            await foreach (var response in replayed.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                // We got here because a previous connection request was answered with continue, so we
                // expect the connection callback to be deterministic and answer the same way again.
                if (response is ContinueProcessingResponsePacket)
                    continue;

                // Unless the packet we are replaying for expects no reply at all, in which case writing
                // one would be read by the MTA as the answer to the next command and put every request
                // and response after it out of step for the rest of the connection. Failing the session
                // costs one message; a desynchronised connection corrupts all of them.
                if (!responseExpected)
                    throw new InvalidOperationException(
                        "Protocol violation: The replayed connection callback returned a non-continue response "
                        + "while handling a packet that expects no reply. Connection callbacks must be deterministic.");

                return AsyncEnumerableEx.Return(response);
            }
        }

        return null;
    }
}
