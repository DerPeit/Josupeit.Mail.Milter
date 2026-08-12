// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The filter is still working; restart the reply timeout (<c>SMFIR_PROGRESS</c>).
/// </summary>
/// <remarks>
/// Answers nothing, so it may be sent as often as needed while the real answer is still being worked out.
/// </remarks>
public sealed class ProgressResponsePacket : ResponsePacket
{
    internal static readonly ProgressResponsePacket Instance = new();

    private ProgressResponsePacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitProgressResponsePacket(this);
    }
}
