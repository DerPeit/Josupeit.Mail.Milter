// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The message is complete (<c>SMFIC_BODYEOB</c>). The last point at which the filter can modify or decide.
/// </summary>
public sealed class EndOfBodyRequestPacket : RequestPacket
{
    /// <summary>
    /// The single shared instance. This packet carries nothing, so there is never a reason for another.
    /// </summary>
    public static readonly EndOfBodyRequestPacket Instance = new();

    private EndOfBodyRequestPacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitEndOfBodyRequestPacket(this);
    }
}
