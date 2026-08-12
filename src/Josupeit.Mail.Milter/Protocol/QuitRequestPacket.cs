// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The MTA is ending the conversation (<c>SMFIC_QUIT</c>). The connection closes after this.
/// </summary>
public sealed class QuitRequestPacket : RequestPacket
{
    internal static readonly QuitRequestPacket Instance = new();

    private QuitRequestPacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitQuitRequestPacket(this);
    }
}