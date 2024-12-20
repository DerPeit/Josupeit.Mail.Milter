namespace Josupeit.Mail.Milter.Protocol;

public abstract class RequestPacket
{
    private protected RequestPacket()
    {
    }

    protected internal abstract bool Accept(RequestPacketVisitor visitor);
}