// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The client sent <c>RCPT TO</c> (<c>SMFIC_RCPT</c>). One per recipient.
/// </summary>
public sealed class RecipientRequestPacket : RequestPacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public RecipientRequestPacket()
    {
    }

    /// <summary>
    /// The envelope recipient as it appeared on the command, angle brackets included.
    /// </summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// The ESMTP parameters that followed the address, each unparsed. Empty when there were none.
    /// </summary>
    public required IReadOnlyList<string> ESmtpArguments { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitRcptToRequestPacket(this);
    }
}