// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Reject the message with an SMTP reply the filter chose (<c>SMFIR_REPLYCODE</c>).
/// </summary>
public sealed class ReplyWithCodeResponsePacket : ResponsePacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public ReplyWithCodeResponsePacket()
    {
    }

    /// <summary>
    /// The status code, as the three digits the wire format expects. A 4xx code defers and a 5xx code rejects.
    /// </summary>
    public required string SmtpCode { get; init; }

    /// <summary>
    /// The text accompanying the code.
    /// </summary>
    public required string Text { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitReplyWithCodeResponsePacket(this);
    }
}
