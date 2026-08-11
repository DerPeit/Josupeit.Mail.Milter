using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class ReplayingAbortedState(IProtocolStateMachine stateMachine, IReadOnlyList<RequestPacket> packetReplayHistory) : ProtocolState
{
    public override IAsyncEnumerable<ResponsePacket> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnBodyChunk, responseExpected: true, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnClientHello, responseExpected: true, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnEndOfBody, responseExpected: true, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnEndOfHeaders, responseExpected: true, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnHeader, responseExpected: true, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnDefineMacro, responseExpected: false, cancellationToken);
    }

    public override async IAsyncEnumerable<ResponsePacket> ProcessQuitting(QuitRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
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
            await stateMachine.TransitionTo(new DisconnectedState()).ConfigureAwait(false);
        }
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessRecipient(RecipientRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnRecipient, responseExpected: true, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, ca => ca.OnSender, responseExpected: true, cancellationToken);
    }

    private async IAsyncEnumerable<ResponsePacket> ProcessRequest<TRequest>(TRequest request, Func<ContextAdapter, Func<TRequest, CancellationToken, IAsyncEnumerable<ResponsePacket>>> processor, bool responseExpected, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var contextAdapter = stateMachine.EnsureContextAdapter();

        try
        {
            // We replay all required packets, because according to the docs, an abortion "resets internal state of milter
            // program to before SMFIC_HELO, but keeps the connection open". However, we know from the internal states, that
            // there can only be macros and the connection request. So we implement this with a little smelly code.
            foreach (var packet in packetReplayHistory)
            {
                var responses = packet switch
                {
                    DefineMacroRequestPacket defineMacroRequestPacket => contextAdapter.OnDefineMacro(defineMacroRequestPacket, cancellationToken),
                    ConnectRequestPacket connectRequestPacket => contextAdapter.OnConnectionEstablished(connectRequestPacket, cancellationToken),
                    _ => null!,
                };

                Debug.Assert(responses != null, "Packet replay history contained unexpected packet.");

                await foreach (var response in responses.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    // We know, by design, that the response represents a commit action. Also, we must have come here
                    // when a previous connection request was responded with a "continue" response. Thus, we expect
                    // the connection attempt to be deterministic and respond with a continue response, again. If it's
                    // a different connection response, we respond with this commit in this stage.
                    if (response is ContinueProcessingResponsePacket)
                        continue;

                    // Unless the packet we are replaying for expects no reply at all, in which case writing
                    // one would be read by the MTA as the answer to the next command and put every
                    // request and response after it out of step for the rest of the connection. Failing
                    // the session costs one message; a desynchronised connection corrupts all of them.
                    if (!responseExpected)
                        throw new InvalidOperationException(
                            "Protocol violation: The replayed connection callback returned a non-continue response "
                            + "while handling a packet that expects no reply. Connection callbacks must be deterministic.");

                    yield return response;
                    yield break;
                }
            }

            await foreach (var response in processor(contextAdapter).Invoke(request, cancellationToken).ConfigureAwait(false))
                yield return response;
        }
        finally
        {
            await stateMachine.TransitionTo(new ReplayingConnectedState(stateMachine, packetReplayHistory)).ConfigureAwait(false);
        }
    }
}