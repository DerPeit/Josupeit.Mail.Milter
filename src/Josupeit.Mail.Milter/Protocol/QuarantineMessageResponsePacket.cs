// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Hold the message in the MTA's queue instead of delivering it (<c>SMFIR_QUARANTINE</c>).
/// </summary>
public sealed class QuarantineMessageResponsePacket : ResponsePacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public QuarantineMessageResponsePacket()
    {
    }

    /// <summary>
    /// Why the message is being held.
    /// </summary>
    public required string Reason { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitQuarantineMessageResponsePacket(this);
    }
}