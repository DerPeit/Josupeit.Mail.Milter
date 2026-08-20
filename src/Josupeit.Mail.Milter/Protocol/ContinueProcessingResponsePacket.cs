// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Carry on with the next event (<c>SMFIR_CONTINUE</c>).
/// </summary>
public sealed class ContinueProcessingResponsePacket : ResponsePacket
{
    /// <summary>
    /// The single shared instance. This packet carries nothing, so there is never a reason for another.
    /// </summary>
    public static readonly ContinueProcessingResponsePacket Instance = new();

    private ContinueProcessingResponsePacket()
    {
    }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitContinueProcessingResponsePacket(this);
    }
}