// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class HeaderRequestPacket : RequestPacket
{
    internal HeaderRequestPacket()
    {
    }

    public required string Name { get; init; }

    public required string Value { get; init; }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitHeaderRequestPacket(this);
    }
}