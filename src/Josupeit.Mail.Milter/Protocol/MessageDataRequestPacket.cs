// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The client sent the SMTP <c>DATA</c> verb and the message content is about to follow
/// (<c>SMFIC_DATA</c>).
/// </summary>
/// <remarks>
/// Carries no payload: the event is the whole message. The MTA only sends it from protocol version 4
/// onwards, and only when the filter did not ask for it to be skipped.
/// </remarks>
public sealed class MessageDataRequestPacket : RequestPacket
{
    /// <summary>
    /// The single shared instance. This packet carries nothing, so there is never a reason for another.
    /// </summary>
    public static readonly MessageDataRequestPacket Instance = new();

    private MessageDataRequestPacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitMessageDataRequestPacket(this);
    }
}
