// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class ConnectedState(IProtocolStateMachine stateMachine) : ProtocolState
{
    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return new(AsyncEnumerable.Empty<ResponsePacket>().Finally(ToAbortedState));

        async Task ToAbortedState()
        {
            await stateMachine.DisposeContextAdapter().ConfigureAwait(false);
            await stateMachine.TransitionTo(new AbortedState(stateMachine)).ConfigureAwait(false);
        }
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnBodyChunk);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnClientHello);
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

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMessageDataStart(MessageDataRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnMessageDataStarted);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessUnknownCommand(UnknownCommandRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => ca.OnUnknownCommand);
    }

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        return ProcessRequest(request, static ca => packet => ca.OnDefineMacro(packet));
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

        var contextAdapter = stateMachine.EnsureContextAdapter();

        return new(processor(contextAdapter).Invoke(request));
    }
}
