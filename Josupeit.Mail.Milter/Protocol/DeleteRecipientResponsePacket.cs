using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class DeleteRecipientResponsePacket : ResponsePacket
{
    internal DeleteRecipientResponsePacket()
    {
    }

    public required string Recipient { get; init; }
    
    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitDeleteRecipientResponsePacket(this);
    }
}