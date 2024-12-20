using System.Collections.Generic;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal interface IProtocolState
{
    bool IsFinal { get; }

    IAsyncEnumerable<ResponsePacket> ProcessAbortion(AbortRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessBodyChunk(BodyChunkRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessClientHello(HeloRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessConnectionAttempt(ConnectRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessEndOfBody(EndOfBodyRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessEndOfHeaders(EndOfHeadersRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessHeader(HeaderRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessMacroDefinition(DefineMacroRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessOptionNegotiationAttempt(OptionNegotiationRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessQuitting(QuitRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessRecipient(RecipientRequestPacket request);

    IAsyncEnumerable<ResponsePacket> ProcessSender(SenderRequestPacket request);
}