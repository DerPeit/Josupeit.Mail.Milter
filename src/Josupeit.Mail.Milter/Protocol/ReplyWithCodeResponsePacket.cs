using System;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class ReplyWithCodeResponsePacket : ResponsePacket
{
    internal ReplyWithCodeResponsePacket()
    {
    }

    public required string SmtpCode { get; init; }

    public required string Text { get; init; }

    protected internal override bool Accept(ResponsePacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitReplyWithCodeResponsePacket(this);
    }
}