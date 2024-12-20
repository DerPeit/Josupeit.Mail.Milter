using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class DeferMessageResponsePacket : ResponsePacket
{
    internal static readonly DeferMessageResponsePacket Instance = new();

    private DeferMessageResponsePacket()
    {
    }

    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitDeferMessageResponsePacket(this);
    }
}