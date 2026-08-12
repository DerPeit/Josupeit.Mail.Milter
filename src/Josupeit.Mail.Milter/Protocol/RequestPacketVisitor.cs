// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

public abstract class RequestPacketVisitor
{
    public virtual bool VisitRequestPacket(RequestPacket requestPacket)
    {
        if (requestPacket == null)
            throw new ArgumentNullException(nameof(requestPacket));

        return requestPacket.Accept(this);
    }

    protected internal virtual bool VisitAbortRequestPacket(AbortRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitBodyChunkRequestPacket(BodyChunkRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitConnectRequestPacket(ConnectRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitDefineMacroRequestPacket(DefineMacroRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitEndOfBodyRequestPacket(EndOfBodyRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitEndOfHeadersRequestPacket(EndOfHeadersRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitHeaderRequestPacket(HeaderRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitHeloRequestPacket(HeloRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitMailFromRequestPacket(SenderRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitOptionsNegotiationRequestPacket(OptionNegotiationRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitQuitRequestPacket(QuitRequestPacket requestPacket)
    {
        return true;
    }

    protected internal virtual bool VisitRcptToRequestPacket(RecipientRequestPacket requestPacket)
    {
        return true;
    }
}