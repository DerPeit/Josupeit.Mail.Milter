using System;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class OptionNegotiationResponsePacket : ResponsePacket
{
    internal OptionNegotiationResponsePacket()
    {
    }

    public required Version MilterProtocolVersion { get; init; }

    public required ActionDiscriminator MilterRequiredActions { get; init; }

    public required ContentBypassDiscriminator MilterUndesiredProtocolContent { get; init; }

    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitOptionNegotiationResponsePacket(this);
    }
}