// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Remove an envelope recipient (<c>SMFIR_DELRCPT</c>).
/// </summary>
public sealed class DeleteRecipientResponsePacket : ResponsePacket
{
    internal DeleteRecipientResponsePacket()
    {
    }

    /// <summary>
    /// The address to remove, spelled as the MTA spelled it.
    /// </summary>
    public required string Recipient { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitDeleteRecipientResponsePacket(this);
    }
}