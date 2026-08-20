// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Accept the message on the wire and then drop it silently (<c>SMFIR_DISCARD</c>).
/// </summary>
public sealed class DiscardMessageResponsePacket : ResponsePacket
{
    /// <summary>
    /// The single shared instance. This packet carries nothing, so there is never a reason for another.
    /// </summary>
    public static readonly DiscardMessageResponsePacket Instance = new();

    private DiscardMessageResponsePacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitDiscardMessageResponsePacket(this);
    }
}