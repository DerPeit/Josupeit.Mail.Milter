// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The header block has ended and the body is about to start (<c>SMFIC_EOH</c>).
/// </summary>
public sealed class EndOfHeadersRequestPacket : RequestPacket
{
    /// <summary>
    /// The single shared instance. This packet carries nothing, so there is never a reason for another.
    /// </summary>
    public static readonly EndOfHeadersRequestPacket Instance = new();

    private EndOfHeadersRequestPacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitEndOfHeadersRequestPacket(this);
    }
}
