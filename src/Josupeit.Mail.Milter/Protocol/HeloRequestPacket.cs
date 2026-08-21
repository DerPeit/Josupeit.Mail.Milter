// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The client greeted the server with <c>HELO</c> or <c>EHLO</c> (<c>SMFIC_HELO</c>).
/// </summary>
public sealed class HeloRequestPacket : RequestPacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public HeloRequestPacket()
    {
    }

    /// <summary>
    /// The name the client announced itself as, unverified.
    /// </summary>
    public required string HeloString { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitHeloRequestPacket(this);
    }
}
