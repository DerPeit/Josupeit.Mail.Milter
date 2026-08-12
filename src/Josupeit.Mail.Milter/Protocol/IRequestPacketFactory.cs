using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

public interface IRequestPacketFactory
{
    RequestPacket CreateRequestPacket(RequestFrame frame);
}