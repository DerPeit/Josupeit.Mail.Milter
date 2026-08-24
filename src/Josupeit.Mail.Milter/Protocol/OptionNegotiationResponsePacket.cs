// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// The filter's half of option negotiation, in reply to the MTA's (<c>SMFIC_OPTNEG</c>).
/// </summary>
public sealed class OptionNegotiationResponsePacket : ResponsePacket
{
    /// <summary>
    /// Creates the packet. Everything it carries is a required property, so an object initializer has to
    /// supply all of them.
    /// </summary>
    public OptionNegotiationResponsePacket()
    {
    }

    /// <summary>
    /// The wire protocol version the filter speaks.
    /// </summary>
    public required Version MilterProtocolVersion { get; init; }

    /// <summary>
    /// The message modifications the filter intends to perform. The MTA refuses any that were not asked for
    /// here.
    /// </summary>
    public required ActionDiscriminator MilterRequiredActions { get; init; }

    /// <summary>
    /// The events the filter does not want. Note the inversion against <see cref="ProtocolRequirements"/>,
    /// which names the events it does want.
    /// </summary>
    public required ContentBypassDiscriminator MilterUndesiredProtocolContent { get; init; }

    /// <summary>
    /// The macros the filter wants, per stage, or <see langword="null"/> to leave the MTA sending its own set.
    /// </summary>
    public MacroRequirements? MilterRequestedMacros { get; init; }

    /// <inheritdoc />
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitOptionNegotiationResponsePacket(this);
    }
}
