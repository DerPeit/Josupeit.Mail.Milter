using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class RejectMessageResponsePacket : ResponsePacket
{
    internal static readonly RejectMessageResponsePacket Instance = new();

    private RejectMessageResponsePacket()
    {
    }

    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitRejectMessageResponsePacket(this);
    }
}