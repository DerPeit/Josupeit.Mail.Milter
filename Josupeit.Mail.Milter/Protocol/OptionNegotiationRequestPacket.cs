using System;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class OptionNegotiationRequestPacket : RequestPacket
{
    internal OptionNegotiationRequestPacket()
    {
    }

    public required Version MtaProtocolVersion { get; init; }

    public required ActionDiscriminator MtaSupportedActions { get; init; }

    public required ContentBypassDiscriminator MtaSupportedContentBypass { get; init; }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitOptionsNegotiationRequestPacket(this);
    }
}