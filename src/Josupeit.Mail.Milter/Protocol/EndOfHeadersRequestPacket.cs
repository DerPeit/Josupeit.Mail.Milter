using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class EndOfHeadersRequestPacket : RequestPacket
{
    internal static readonly EndOfHeadersRequestPacket Instance = new();

    private EndOfHeadersRequestPacket()
    {
    }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitEndOfHeadersRequestPacket(this);
    }
}