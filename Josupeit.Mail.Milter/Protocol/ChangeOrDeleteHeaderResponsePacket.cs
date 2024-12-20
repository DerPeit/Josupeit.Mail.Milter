using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class ChangeOrDeleteHeaderResponsePacket : ResponsePacket
{
    internal ChangeOrDeleteHeaderResponsePacket()
    {
    }

    public required uint Index { get; init; }

    public required string Name { get; init; }

    public required string Value { get; init; }
    
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitChangeOrDeleteHeaderResponsePacket(this);
    }
}