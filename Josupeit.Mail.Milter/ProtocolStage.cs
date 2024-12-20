namespace Josupeit.Mail.Milter;

public enum ProtocolStage
{
    Connect,
    ClientHello,
    Sender,
    Recipient,
    Header,
    EndOfHeaders,
    Body,
    EndOfBody,
}