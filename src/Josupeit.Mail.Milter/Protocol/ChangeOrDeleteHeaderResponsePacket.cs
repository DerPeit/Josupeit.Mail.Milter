// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Change a header, or remove it by changing it to an empty value (<c>SMFIR_CHGHEADER</c>).
/// </summary>
/// <remarks>
/// One packet covers both because the protocol has no separate deletion: an empty <see cref="Value"/> is what
/// removes a header.
/// </remarks>
public sealed class ChangeOrDeleteHeaderResponsePacket : ResponsePacket
{
    internal ChangeOrDeleteHeaderResponsePacket()
    {
    }

    /// <summary>
    /// Which occurrence of <see cref="Name"/> to act on, counting from <em>one</em>.
    /// </summary>
    /// <remarks>
    /// The wire numbering, not the caller-facing one. <see cref="ChangeHeaderAction"/> and
    /// <see cref="DeleteHeaderAction"/> count from zero and are translated on the way here.
    /// </remarks>
    public required uint Index { get; init; }

    /// <summary>
    /// The header name, without the separating colon.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The new value, or empty to remove the header.
    /// </summary>
    public required string Value { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitChangeOrDeleteHeaderResponsePacket(this);
    }
}
