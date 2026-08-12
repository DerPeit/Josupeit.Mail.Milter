using System;
using System.Collections.Generic;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class SenderRequestPacket : RequestPacket
{
    internal SenderRequestPacket()
    {
    }

    public required string Sender { get; init; }

    public required IReadOnlyList<string> ESmtpArguments { get; init; }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitMailFromRequestPacket(this);
    }
}