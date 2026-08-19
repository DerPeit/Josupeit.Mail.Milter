// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Add an envelope recipient together with its ESMTP arguments (<c>SMFIR_ADDRCPT_PAR</c>).
/// </summary>
public sealed class AddRecipientWithParametersResponsePacket : ResponsePacket
{
    internal AddRecipientWithParametersResponsePacket()
    {
    }

    /// <summary>
    /// The address to add.
    /// </summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// The ESMTP arguments to send with it.
    /// </summary>
    public required string ESmtpArguments { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitAddRecipientWithParametersResponsePacket(this);
    }
}
