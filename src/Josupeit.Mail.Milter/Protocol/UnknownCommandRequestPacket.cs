// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The client sent an SMTP command the MTA does not recognise (<c>SMFIC_UNKNOWN</c>).
/// </summary>
/// <remarks>
/// The MTA only sends this from protocol version 3 onwards, and only when the filter did not ask for
/// it to be skipped.
/// </remarks>
public sealed class UnknownCommandRequestPacket : RequestPacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public UnknownCommandRequestPacket()
    {
    }

    /// <summary>
    /// The command line exactly as the client sent it.
    /// </summary>
    /// <remarks>
    /// Unparsed and unvalidated, verb and arguments together, because the MTA had no grammar to apply
    /// to it. Treat it as attacker-controlled text.
    /// </remarks>
    public required string CommandText { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitUnknownCommandRequestPacket(this);
    }
}
