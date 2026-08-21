// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The MTA's half of option negotiation (<c>SMFIC_OPTNEG</c>).
/// </summary>
/// <remarks>
/// The first packet on every connection. It states what the MTA can do; the filter answers with what it needs,
/// and the connection fails if the MTA cannot supply it.
/// </remarks>
public sealed class OptionNegotiationRequestPacket : RequestPacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public OptionNegotiationRequestPacket()
    {
    }

    /// <summary>
    /// The wire protocol version the MTA offers. This library speaks version 2.
    /// </summary>
    public required Version MtaProtocolVersion { get; init; }

    /// <summary>
    /// The message modifications the MTA is willing to perform.
    /// </summary>
    public required ActionDiscriminator MtaSupportedActions { get; init; }

    /// <summary>
    /// The events the MTA is willing to skip.
    /// </summary>
    public required ContentBypassDiscriminator MtaSupportedContentBypass { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitOptionsNegotiationRequestPacket(this);
    }
}
