using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

public interface IResponseFrameFactory
{
    ResponseFrame CreateResponseFrame(ResponsePacket response);
}