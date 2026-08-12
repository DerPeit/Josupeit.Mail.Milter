// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Accept the message and send no further events about it (<c>SMFIR_ACCEPT</c>).
/// </summary>
public sealed class AcceptMessageResponsePacket : ResponsePacket
{
    internal static readonly AcceptMessageResponsePacket Instance = new();

    private AcceptMessageResponsePacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitAcceptMessageResponsePacket(this);
    }
}