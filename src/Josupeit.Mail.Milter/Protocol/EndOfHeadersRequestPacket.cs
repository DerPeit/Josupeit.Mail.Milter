// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The header block has ended and the body is about to start (<c>SMFIC_EOH</c>).
/// </summary>
public sealed class EndOfHeadersRequestPacket : RequestPacket
{
    internal static readonly EndOfHeadersRequestPacket Instance = new();

    private EndOfHeadersRequestPacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitEndOfHeadersRequestPacket(this);
    }
}