// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// One header of the message under inspection (<c>SMFIC_HEADER</c>).
/// </summary>
public sealed class HeaderRequestPacket : RequestPacket
{
    internal HeaderRequestPacket()
    {
    }

    /// <summary>
    /// The header name, without the separating colon.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The header value, unfolded if it spanned several lines.
    /// </summary>
    public required string Value { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitHeaderRequestPacket(this);
    }
}