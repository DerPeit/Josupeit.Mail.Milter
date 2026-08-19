// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Dispatches a <see cref="RequestPacket"/> to a method named for its type.
/// </summary>
/// <remarks>
/// Every method has a base implementation that returns <see langword="true"/>, so a derived visitor
/// overrides only the packets it cares about. Adding a packet type to the library means adding a
/// <c>Visit</c> method here, which is a breaking change for anyone who has derived from this - worth
/// weighing before a packet type is added.
/// </remarks>
public abstract class RequestPacketVisitor
{
    /// <summary>
    /// Entry point: hands the packet to the method matching its type.
    /// </summary>
    /// <param name="requestPacket">The packet to visit.</param>
    /// <returns>Whatever the matching method returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requestPacket"/> is
    /// <see langword="null"/>.</exception>
    public virtual bool VisitRequestPacket(RequestPacket requestPacket)
    {
        if (requestPacket == null)
            throw new ArgumentNullException(nameof(requestPacket));

        return requestPacket.Accept(this);
    }

    /// <summary>
    /// Called for a <see cref="AbortRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitAbortRequestPacket(AbortRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="BodyChunkRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitBodyChunkRequestPacket(BodyChunkRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="ConnectRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitConnectRequestPacket(ConnectRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="DefineMacroRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitDefineMacroRequestPacket(DefineMacroRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="EndOfBodyRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitEndOfBodyRequestPacket(EndOfBodyRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="EndOfHeadersRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitEndOfHeadersRequestPacket(EndOfHeadersRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="HeaderRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitHeaderRequestPacket(HeaderRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="HeloRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitHeloRequestPacket(HeloRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="SenderRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitMailFromRequestPacket(SenderRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="MessageDataRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitMessageDataRequestPacket(MessageDataRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="OptionNegotiationRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitOptionsNegotiationRequestPacket(OptionNegotiationRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="QuitRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitQuitRequestPacket(QuitRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="QuitForNewConnectionRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitQuitForNewConnectionRequestPacket(QuitForNewConnectionRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="RecipientRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitRcptToRequestPacket(RecipientRequestPacket requestPacket)
    {
        return true;
    }

    /// <summary>
    /// Called for a <see cref="UnknownCommandRequestPacket"/>. The base implementation does nothing.
    /// </summary>
    /// <param name="requestPacket">The packet being visited.</param>
    /// <returns><see langword="true"/> to carry on visiting; <see langword="false"/> to stop.</returns>
    protected internal virtual bool VisitUnknownCommandRequestPacket(UnknownCommandRequestPacket requestPacket)
    {
        return true;
    }
}
