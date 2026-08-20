// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The SMTP connection has ended, but the milter connection stays open for the next one
/// (<c>SMFIC_QUIT_NC</c>).
/// </summary>
/// <remarks>
/// Carries no payload and expects no reply. A fresh <see cref="ConnectRequestPacket"/> for a different
/// client follows on the same socket, so everything the filter was told about the previous one stops
/// being true. The MTA only sends this from protocol version 6 onwards.
/// </remarks>
public sealed class QuitForNewConnectionRequestPacket : RequestPacket
{
    /// <summary>
    /// The single shared instance. This packet carries nothing, so there is never a reason for another.
    /// </summary>
    public static readonly QuitForNewConnectionRequestPacket Instance = new();

    private QuitForNewConnectionRequestPacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitQuitForNewConnectionRequestPacket(this);
    }
}
