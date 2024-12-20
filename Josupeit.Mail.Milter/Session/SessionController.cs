using System.Collections.Generic;
using System.Diagnostics;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session;

internal sealed class SessionController(ISessionControlee controlee) : RequestPacketVisitor
{
    private IAsyncEnumerable<ResponsePacket>? _responses;

    public IAsyncEnumerable<ResponsePacket> ProcessRequest(RequestPacket request)
    {
        var visited = VisitRequestPacket(request);

        Debug.Assert(visited);

        return _responses!;
    }

    protected internal override bool VisitAbortRequestPacket(AbortRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessAbortion(requestPacket);

        return true;
    }

    protected internal override bool VisitBodyChunkRequestPacket(BodyChunkRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessBodyChunk(requestPacket);

        return true;
    }

    protected internal override bool VisitConnectRequestPacket(ConnectRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessConnectionAttempt(requestPacket);

        return true;
    }

    protected internal override bool VisitDefineMacroRequestPacket(DefineMacroRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessMacroDefinition(requestPacket);
        
        return true;
    }

    protected internal override bool VisitEndOfBodyRequestPacket(EndOfBodyRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessEndOfBody(requestPacket);

        return true;
    }

    protected internal override bool VisitEndOfHeadersRequestPacket(EndOfHeadersRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessEndOfHeaders(requestPacket);

        return true;
    }

    protected internal override bool VisitHeaderRequestPacket(HeaderRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessHeader(requestPacket);

        return true;
    }

    protected internal override bool VisitHeloRequestPacket(HeloRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessClientHello(requestPacket);

        return true;
    }

    protected internal override bool VisitMailFromRequestPacket(SenderRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessSender(requestPacket);

        return true;
    }

    protected internal override bool VisitOptionsNegotiationRequestPacket(OptionNegotiationRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessOptionNegotiationAttempt(requestPacket);

        return true;
    }

    protected internal override bool VisitQuitRequestPacket(QuitRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessQuitting(requestPacket);

        return true;
    }

    protected internal override bool VisitRcptToRequestPacket(RecipientRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessRecipient(requestPacket);

        return true;
    }
}