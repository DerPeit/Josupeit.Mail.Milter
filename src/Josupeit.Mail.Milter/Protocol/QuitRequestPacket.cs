// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class QuitRequestPacket : RequestPacket
{
    internal static readonly QuitRequestPacket Instance = new();

    private QuitRequestPacket()
    {
    }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitQuitRequestPacket(this);
    }
}