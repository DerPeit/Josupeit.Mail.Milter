// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Add an envelope recipient (<c>SMFIR_ADDRCPT</c>).
/// </summary>
public sealed class AddRecipientResponsePacket : ResponsePacket
{
    internal AddRecipientResponsePacket()
    {
    }

    /// <summary>
    /// The address to add.
    /// </summary>
    public required string Recipient { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitAddRecipientResponsePacket(this);
    }
}