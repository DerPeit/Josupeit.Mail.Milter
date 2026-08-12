using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class EndOfBodyRequestPacket : RequestPacket
{
    internal static readonly EndOfBodyRequestPacket Instance = new();

    private EndOfBodyRequestPacket()
    {
    }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitEndOfBodyRequestPacket(this);
    }
}