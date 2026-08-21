// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Macro values the MTA supplied ahead of an event (<c>SMFIC_MACRO</c>).
/// </summary>
/// <remarks>
/// The only request expecting no reply. Answering it anyway desynchronises the connection for good.
/// </remarks>
public sealed class DefineMacroRequestPacket : RequestPacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public DefineMacroRequestPacket()
    {
    }

    /// <summary>
    /// The command these values were sent for.
    /// </summary>
    public required CommandCode CommandCode { get; init; }

    /// <summary>
    /// The values, keyed by macro name.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Macros { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitDefineMacroRequestPacket(this);
    }
}
