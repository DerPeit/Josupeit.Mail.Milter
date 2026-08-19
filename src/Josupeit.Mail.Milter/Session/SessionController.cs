// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session;

internal sealed class SessionController(ISessionControlee controlee) : RequestPacketVisitor
{
    private ValueTask<IAsyncEnumerable<ResponsePacket>> _responses;
    private CancellationToken _cancellationToken;

    public ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRequest(RequestPacket request, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        var visited = VisitRequestPacket(request);

        Debug.Assert(visited);

        return _responses;
    }

    protected internal override bool VisitAbortRequestPacket(AbortRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessAbortion(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitBodyChunkRequestPacket(BodyChunkRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessBodyChunk(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitConnectRequestPacket(ConnectRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessConnectionAttempt(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitDefineMacroRequestPacket(DefineMacroRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessMacroDefinition(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitEndOfBodyRequestPacket(EndOfBodyRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessEndOfBody(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitEndOfHeadersRequestPacket(EndOfHeadersRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessEndOfHeaders(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitHeaderRequestPacket(HeaderRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessHeader(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitHeloRequestPacket(HeloRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessClientHello(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitMailFromRequestPacket(SenderRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessSender(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitMessageDataRequestPacket(MessageDataRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessMessageDataStart(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitOptionsNegotiationRequestPacket(OptionNegotiationRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessOptionNegotiationAttempt(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitQuitRequestPacket(QuitRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessQuitting(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitQuitForNewConnectionRequestPacket(QuitForNewConnectionRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessQuittingForNewConnection(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitRcptToRequestPacket(RecipientRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessRecipient(requestPacket, _cancellationToken);

        return true;
    }

    protected internal override bool VisitUnknownCommandRequestPacket(UnknownCommandRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessUnknownCommand(requestPacket, _cancellationToken);

        return true;
    }
}