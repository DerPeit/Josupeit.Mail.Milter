// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Reject the message permanently (<c>SMFIR_REJECT</c>).
/// </summary>
public sealed class RejectMessageResponsePacket : ResponsePacket
{
    internal static readonly RejectMessageResponsePacket Instance = new();

    private RejectMessageResponsePacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitRejectMessageResponsePacket(this);
    }
}