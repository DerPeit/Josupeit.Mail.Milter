// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Replace the envelope sender (<c>SMFIR_CHGFROM</c>).
/// </summary>
public sealed class ChangeSenderResponsePacket : ResponsePacket
{
    internal ChangeSenderResponsePacket()
    {
    }

    /// <summary>
    /// The address to become the new envelope sender.
    /// </summary>
    public required string Sender { get; init; }

    /// <summary>
    /// The ESMTP arguments to send with it, or <see langword="null"/> when there are none.
    /// </summary>
    /// <remarks>
    /// The field is left off the frame entirely when this is absent, rather than written as an empty
    /// string: the MTA reads it only if bytes remain.
    /// </remarks>
    public string? ESmtpArguments { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitChangeSenderResponsePacket(this);
    }
}
