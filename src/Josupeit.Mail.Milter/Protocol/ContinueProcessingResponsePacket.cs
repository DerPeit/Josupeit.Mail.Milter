// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class ContinueProcessingResponsePacket : ResponsePacket
{
    internal static readonly ContinueProcessingResponsePacket Instance = new();

    private ContinueProcessingResponsePacket()
    {
    }

    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitContinueProcessingResponsePacket(this);
    }
}