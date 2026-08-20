// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Stop sending any more of the event just answered (<c>SMFIR_SKIP</c>).
/// </summary>
public sealed class SkipRemainingEventsResponsePacket : ResponsePacket
{
    internal static readonly SkipRemainingEventsResponsePacket Instance = new();

    private SkipRemainingEventsResponsePacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitSkipRemainingEventsResponsePacket(this);
    }
}
