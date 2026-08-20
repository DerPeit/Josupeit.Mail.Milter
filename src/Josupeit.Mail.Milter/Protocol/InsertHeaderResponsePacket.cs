// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Insert a header at a chosen position (<c>SMFIR_INSHEADER</c>).
/// </summary>
public sealed class InsertHeaderResponsePacket : ResponsePacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public InsertHeaderResponsePacket()
    {
    }

    /// <summary>
    /// How many headers the new one is placed after, counting from zero.
    /// </summary>
    /// <remarks>
    /// Zero puts it above every existing header. The wire counts from one, and that translation happens
    /// when the frame is written.
    /// </remarks>
    public required uint Index { get; init; }

    /// <summary>
    /// The name of the header to insert, without the separating colon.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The value of the header to insert.
    /// </summary>
    public required string Value { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitInsertHeaderResponsePacket(this);
    }
}
