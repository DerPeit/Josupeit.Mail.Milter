using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class AddHeaderResponsePacket : ResponsePacket
{
    internal AddHeaderResponsePacket()
    {
    }

    public required string Name { get; init; }

    public required string Value { get; init; }

    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitAddHeaderResponsePacket(this);
    }
}