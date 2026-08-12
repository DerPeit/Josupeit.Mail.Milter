// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Append a header to the message (<c>SMFIR_ADDHEADER</c>).
/// </summary>
public sealed class AddHeaderResponsePacket : ResponsePacket
{
    internal AddHeaderResponsePacket()
    {
    }

    /// <summary>
    /// The header name, without the separating colon.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The header value.
    /// </summary>
    public required string Value { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitAddHeaderResponsePacket(this);
    }
}