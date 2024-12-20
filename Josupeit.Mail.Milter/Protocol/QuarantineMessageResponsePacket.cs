using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class QuarantineMessageResponsePacket : ResponsePacket
{
    internal QuarantineMessageResponsePacket()
    {
    }

    public required string Reason { get; init; }
    
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitQuarantineMessageResponsePacket(this);
    }
}