using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class AbortedState(IProtocolStateMachine stateMachine) : ProtocolState
{
    public override IAsyncEnumerable<ResponsePacket> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnBodyChunk, cancellationToken);
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnClientHello, cancellationToken);
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

    // A macro definition carries no message content, so it does not start the next message and does not
    // transition. The replaying counterpart cannot follow that rule: replaying is what moves it on.
    public override IAsyncEnumerable<ResponsePacket> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return Enumerate(CancellationToken.None);

        async IAsyncEnumerable<ResponsePacket> Enumerate([EnumeratorCancellation] CancellationToken enumeratorToken)
        {
            var contextAdapter = stateMachine.EnsureContextAdapter();

            await foreach (var response in contextAdapter.OnDefineMacro(request, enumeratorToken).ConfigureAwait(false))
                yield return response;
        }
    }

    public override IAsyncEnumerable<ResponsePacket> ProcessQuitting(QuitRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return Enumerate();

        async IAsyncEnumerable<ResponsePacket> Enumerate()
        {
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

        return Enumerate(CancellationToken.None);

        async IAsyncEnumerable<ResponsePacket> Enumerate([EnumeratorCancellation] CancellationToken enumeratorToken)
        {
            var contextAdapter = stateMachine.EnsureContextAdapter();

            try
            {
                await foreach (var response in processor(contextAdapter).Invoke(request, enumeratorToken).ConfigureAwait(false))
                    yield return response;
            }
            finally
            {
                await stateMachine.TransitionTo(new ConnectedState(stateMachine)).ConfigureAwait(false);
            }
        }
    }
}
