// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Reject the message temporarily so the sender retries (<c>SMFIR_TEMPFAIL</c>).
/// </summary>
public sealed class DeferMessageResponsePacket : ResponsePacket
{
    internal static readonly DeferMessageResponsePacket Instance = new();

    private DeferMessageResponsePacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitDeferMessageResponsePacket(this);
    }
}