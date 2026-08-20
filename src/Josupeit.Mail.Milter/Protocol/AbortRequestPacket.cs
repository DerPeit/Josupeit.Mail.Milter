// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The MTA abandoned the message it was filtering (<c>SMFIC_ABORT</c>).
/// </summary>
/// <remarks>
/// The connection stays open and the next message arrives on it, so this resets the filter rather than
/// ending it. No reply is expected.
/// </remarks>
public sealed class AbortRequestPacket : RequestPacket
{
    /// <summary>
    /// The single shared instance. This packet carries nothing, so there is never a reason for another.
    /// </summary>
    public static readonly AbortRequestPacket Instance = new();

    private AbortRequestPacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitAbortRequestPacket(this);
    }
}