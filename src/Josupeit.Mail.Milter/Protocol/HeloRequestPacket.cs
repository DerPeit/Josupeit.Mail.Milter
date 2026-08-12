// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class HeloRequestPacket : RequestPacket
{
    internal HeloRequestPacket()
    {
    }

    public required string HeloString { get; init; }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitHeloRequestPacket(this);
    }
}