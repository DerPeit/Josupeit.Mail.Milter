// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Remove an envelope recipient (<c>SMFIR_DELRCPT</c>).
/// </summary>
public sealed class DeleteRecipientResponsePacket : ResponsePacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public DeleteRecipientResponsePacket()
    {
    }

    /// <summary>
    /// The address to remove, spelled as the MTA spelled it.
    /// </summary>
    public required string Recipient { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitDeleteRecipientResponsePacket(this);
    }
}
