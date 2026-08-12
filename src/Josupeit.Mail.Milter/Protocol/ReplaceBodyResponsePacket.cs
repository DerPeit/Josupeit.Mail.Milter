// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class ReplaceBodyResponsePacket : ResponsePacket
{
    internal ReplaceBodyResponsePacket()
    {
    }

    public required IMemoryOwner<byte> Memory { get; init; }

    public required Range BodyRange { get; init; }

    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitReplaceBodyResponsePacket(this);
    }
}