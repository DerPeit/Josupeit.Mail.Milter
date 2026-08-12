// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

public abstract class ResponsePacketVisitor
{
    public virtual bool VisitResponsePacket(ResponsePacket responsePacket)
    {
        if (responsePacket == null)
            throw new ArgumentNullException(nameof(responsePacket));

        return responsePacket.Accept(this);
    }

    protected internal virtual bool VisitAcceptMessageResponsePacket(AcceptMessageResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitAddHeaderResponsePacket(AddHeaderResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitAddRecipientResponsePacket(AddRecipientResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitChangeOrDeleteHeaderResponsePacket(ChangeOrDeleteHeaderResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitContinueProcessingResponsePacket(ContinueProcessingResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitDeferMessageResponsePacket(DeferMessageResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitDeleteRecipientResponsePacket(DeleteRecipientResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitDiscardMessageResponsePacket(DiscardMessageResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitOptionNegotiationResponsePacket(OptionNegotiationResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitProgressResponsePacket(ProgressResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitQuarantineMessageResponsePacket(QuarantineMessageResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitRejectMessageResponsePacket(RejectMessageResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitReplaceBodyResponsePacket(ReplaceBodyResponsePacket responsePacket)
    {
        return true;
    }

    protected internal virtual bool VisitReplyWithCodeResponsePacket(ReplyWithCodeResponsePacket responsePacket)
    {
        return true;
    }
}