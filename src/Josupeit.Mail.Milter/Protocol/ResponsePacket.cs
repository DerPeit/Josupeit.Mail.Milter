namespace Josupeit.Mail.Milter.Protocol;

public abstract class ResponsePacket
{
    private protected ResponsePacket()
    {
    }

    protected internal abstract bool Accept(ResponsePacketVisitor visitor);
}