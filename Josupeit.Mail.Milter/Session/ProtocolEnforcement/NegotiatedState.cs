using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class NegotiatedState(IProtocolStateMachine stateMachine) : ProtocolState
{
    // An MTA is in a transaction from the moment it begins a connection event, which is before it decides
    // whether to deliver one to us at all. So a filter that requires neither Connect nor ClientHello sits
    // in this state and still receives an abortion - Postfix sends one on RSET, on a repeated EHLO and on
    // STARTTLS. Nothing message-specific has reached the context here yet, so resetting to before HELO is
    // just dropping it; the state itself already is that point.
    public override async ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        await stateMachine.DisposeContextAdapter().ConfigureAwait(false);

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnBodyChunk, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnClientHello, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessConnectionAttempt(ConnectRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnConnectionEstablished, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnEndOfBody, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnEndOfHeaders, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnHeader, cancellationToken);
    }

    // A macro definition carries no message content, so it neither starts the next message nor transitions.
    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var contextAdapter = stateMachine.EnsureContextAdapter();

        return new(contextAdapter.OnDefineMacro(request, cancellationToken));
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
        return ProcessRequest(request, static ca => ca.OnRecipient, cancellationToken);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnSender, cancellationToken);
    }

    private ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRequest<TRequest>(TRequest request, Func<ContextAdapter, Func<TRequest, CancellationToken, IAsyncEnumerable<ResponsePacket>>> processor, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var contextAdapter = stateMachine.EnsureContextAdapter();
        var responses = processor(contextAdapter).Invoke(request, cancellationToken);

        return new(responses.Finally(ToConnectedState));

        async Task ToConnectedState() => await stateMachine.TransitionTo(new ConnectedState(stateMachine)).ConfigureAwait(false);
    }
}
