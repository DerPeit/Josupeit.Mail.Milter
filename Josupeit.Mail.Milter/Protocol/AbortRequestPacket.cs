using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class AbortRequestPacket : RequestPacket
{
    internal static readonly AbortRequestPacket Instance = new();

    private AbortRequestPacket()
    {
    }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitAbortRequestPacket(this);
    }
}