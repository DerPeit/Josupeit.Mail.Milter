using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal abstract class ProtocolState : IProtocolState
{
    public virtual bool IsFinal => false;

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessConnectionAttempt(ConnectRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessOptionNegotiationAttempt(OptionNegotiationRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessQuitting(QuitRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRecipient(RecipientRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    public virtual ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        return RejectUnexpected(request);
    }

    [DoesNotReturn]
    protected static void ThrowInvalidPacketReceived()
    {
        throw new InvalidOperationException("Protocol violation: Unexpected packet received.");
    }

    private static ValueTask<IAsyncEnumerable<ResponsePacket>> RejectUnexpected(RequestPacket request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return new(AsyncEnumerable.Empty<ResponsePacket>());
    }
}
