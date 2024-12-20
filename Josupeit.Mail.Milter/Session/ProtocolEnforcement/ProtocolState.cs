using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal abstract partial class ProtocolState : IProtocolState
{
    public virtual bool IsFinal => false;

    public virtual IAsyncEnumerable<ResponsePacket> ProcessAbortion(AbortRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessBodyChunk(BodyChunkRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessClientHello(HeloRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessConnectionAttempt(ConnectRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessEndOfHeaders(EndOfHeadersRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessHeader(HeaderRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessMacroDefinition(DefineMacroRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }
    
    public virtual IAsyncEnumerable<ResponsePacket> ProcessOptionNegotiationAttempt(OptionNegotiationRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessQuitting(QuitRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessRecipient(RecipientRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    public virtual IAsyncEnumerable<ResponsePacket> ProcessSender(SenderRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowInvalidPacketReceived();

        return AsyncEnumerable.Empty<ResponsePacket>();
    }

    [DoesNotReturn]
    protected static void ThrowInvalidPacketReceived()
    {
        throw new InvalidOperationException("Protocol violation: Unexpected packet received.");
    }
}

partial class ProtocolState
{
    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessAbortion(AbortRequestPacket request)
    {
        return ProcessAbortion(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessBodyChunk(BodyChunkRequestPacket request)
    {
        return ProcessBodyChunk(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessClientHello(HeloRequestPacket request)
    {
        return ProcessClientHello(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessConnectionAttempt(ConnectRequestPacket request)
    {
        return ProcessConnectionAttempt(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessEndOfBody(EndOfBodyRequestPacket request)
    {
        return ProcessEndOfBody(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessEndOfHeaders(EndOfHeadersRequestPacket request)
    {
        return ProcessEndOfHeaders(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessHeader(HeaderRequestPacket request)
    {
        return ProcessHeader(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessMacroDefinition(DefineMacroRequestPacket request)
    {
        return ProcessMacroDefinition(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessOptionNegotiationAttempt(OptionNegotiationRequestPacket request)
    {
        return ProcessOptionNegotiationAttempt(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessQuitting(QuitRequestPacket request)
    {
        return ProcessQuitting(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessRecipient(RecipientRequestPacket request)
    {
        return ProcessRecipient(request, CancellationToken.None);
    }

    IAsyncEnumerable<ResponsePacket> IProtocolState.ProcessSender(SenderRequestPacket request)
    {
        return ProcessSender(request, CancellationToken.None);
    }
}
