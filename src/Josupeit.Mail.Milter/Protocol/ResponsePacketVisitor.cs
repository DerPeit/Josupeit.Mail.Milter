// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Dispatches a <see cref="ResponsePacket"/> to a method named for its type.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="RequestPacketVisitor"/>, with the same rules: base implementations
/// return <see langword="true"/>, and adding a packet type means adding a method here.
/// </remarks>
public abstract class ResponsePacketVisitor
{
    /// <summary>
    /// Entry point: hands the packet to the method matching its type.
    /// </summary>
    /// <param name="responsePacket">The packet to visit.</param>
    /// <returns>Whatever the matching method returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="responsePacket"/> is
    /// <see langword="null"/>.</exception>
    public virtual bool VisitResponsePacket(ResponsePacket responsePacket)
    {
        ArgumentNullException.ThrowIfNull(responsePacket);

        return responsePacket.Accept(this);
    }

    /// <summary>
    /// Called for a <see cref="AcceptMessageResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitAcceptMessageResponsePacket(AcceptMessageResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="AddHeaderResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitAddHeaderResponsePacket(AddHeaderResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="AddRecipientResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitAddRecipientResponsePacket(AddRecipientResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="ChangeOrDeleteHeaderResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitChangeOrDeleteHeaderResponsePacket(ChangeOrDeleteHeaderResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="ChangeSenderResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitChangeSenderResponsePacket(ChangeSenderResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="ContinueProcessingResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitContinueProcessingResponsePacket(ContinueProcessingResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="DeferMessageResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitDeferMessageResponsePacket(DeferMessageResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="DeleteRecipientResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitDeleteRecipientResponsePacket(DeleteRecipientResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="DiscardMessageResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitDiscardMessageResponsePacket(DiscardMessageResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="InsertHeaderResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitInsertHeaderResponsePacket(InsertHeaderResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="OptionNegotiationResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitOptionNegotiationResponsePacket(OptionNegotiationResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="ProgressResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitProgressResponsePacket(ProgressResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="QuarantineMessageResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitQuarantineMessageResponsePacket(QuarantineMessageResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="RejectMessageResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitRejectMessageResponsePacket(RejectMessageResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="ReplaceBodyResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitReplaceBodyResponsePacket(ReplaceBodyResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="ReplyWithCodeResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitReplyWithCodeResponsePacket(ReplyWithCodeResponsePacket responsePacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="SkipRemainingEventsResponsePacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="responsePacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitSkipRemainingEventsResponsePacket(SkipRemainingEventsResponsePacket responsePacket)
    {
        return true;
    }
}
