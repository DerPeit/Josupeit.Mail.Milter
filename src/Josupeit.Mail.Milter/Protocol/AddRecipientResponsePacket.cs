// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Add an envelope recipient, with or without ESMTP arguments (<c>SMFIR_ADDRCPT</c> and
/// <c>SMFIR_ADDRCPT_PAR</c>).
/// </summary>
/// <remarks>
/// One packet for two response codes, because they carry the same instruction and differ only in
/// whether the arguments field is present. Which code goes on the wire follows from
/// <see cref="ESmtpArguments"/>, and <see cref="ResponseFrameFactory"/> is where that is decided.
/// </remarks>
public sealed class AddRecipientResponsePacket : ResponsePacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public AddRecipientResponsePacket()
    {
    }

    /// <summary>
    /// The address to add.
    /// </summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// The ESMTP arguments to send with it, or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    /// Absent rather than empty. Arguments make this an <c>SMFIR_ADDRCPT_PAR</c>, which the MTA grants
    /// as a right of its own - so a recipient that needs none is sent as a plain <c>SMFIR_ADDRCPT</c>
    /// and asks for nothing beyond <see cref="ActionRequirements.AddRecipients"/>.
    /// </remarks>
    public string? ESmtpArguments { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitAddRecipientResponsePacket(this);
    }
}
