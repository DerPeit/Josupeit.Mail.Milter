// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Replace a chunk of the message body (<c>SMFIR_REPLBODY</c>).
/// </summary>
/// <remarks>
/// One packet per chunk: a replacement body larger than a single frame becomes several of these in sequence.
/// </remarks>
public sealed class ReplaceBodyResponsePacket : ResponsePacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public ReplaceBodyResponsePacket()
    {
    }

    /// <summary>
    /// The rented memory holding the chunk. Its consumer disposes it once the frame is written.
    /// </summary>
    public required IMemoryOwner<byte> Memory { get; init; }

    /// <summary>
    /// The range within <see cref="Memory"/> that the chunk occupies.
    /// </summary>
    public required Range BodyRange { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitReplaceBodyResponsePacket(this);
    }
}