using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session;

internal sealed class ContextAdapter(Func<IMilterCallbacks> contextFactory) : IAsyncDisposable
{
    private IMilterCallbacks? _context;

    public ValueTask DisposeAsync()
    {
        switch (_context)
        {
            case IAsyncDisposable asyncDisposable:
                return asyncDisposable.DisposeAsync();

            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        return new();
    }

    public async IAsyncEnumerable<ResponsePacket> OnDefineMacro(DefineMacroRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var protocolStage = request.CommandCode switch
        {
            CommandCode.Connect => ProtocolStage.Connect,
            CommandCode.Helo => ProtocolStage.ClientHello,
            CommandCode.Sender => ProtocolStage.Sender,
            CommandCode.Recipient => ProtocolStage.Recipient,
            CommandCode.Header => ProtocolStage.Header,
            CommandCode.EndOfHeaders => ProtocolStage.EndOfHeaders,
            CommandCode.BodyChunk => ProtocolStage.Body,
            CommandCode.EndOfBody => ProtocolStage.EndOfBody,
            _ => (ProtocolStage)(-1),
        };

        if (!Enum.IsDefined(protocolStage))
            yield break;

        var info = new MacroDefinitionInformation(protocolStage, request.Macros);
        var context = _context ??= contextFactory();

        await context.OnDefineMacro(info, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ResponsePacket> OnConnectionEstablished(ConnectRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var info = new ConnectionInformation(request.HostName, request.EndPoint);
        var context = _context ??= contextFactory();

        var commitAction = await context.OnConnectionEstablished(info, cancellationToken).ConfigureAwait(false);
        var responses = commitAction.CreateResponsePackets().ConfigureAwait(false).WithCancellation(cancellationToken);

        await foreach (var response in responses.ConfigureAwait(false))
            yield return response;
    }

    public async IAsyncEnumerable<ResponsePacket> OnClientHello(HeloRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var info = new ClientHelloInformation(request.HeloString);
        var context = _context ??= contextFactory();

        var commitAction = await context.OnClientHello(info, cancellationToken).ConfigureAwait(false);
        var responses = commitAction.CreateResponsePackets().ConfigureAwait(false).WithCancellation(cancellationToken);

        await foreach (var response in responses.ConfigureAwait(false))
            yield return response;
    }

    public async IAsyncEnumerable<ResponsePacket> OnSender(SenderRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var info = new SenderRecipientInformation(request.Sender, request.ESmtpArguments);
        var context = _context ??= contextFactory();

        var commitAction = await context.OnSender(info, cancellationToken).ConfigureAwait(false);
        var responses = commitAction.CreateResponsePackets().ConfigureAwait(false).WithCancellation(cancellationToken);

        await foreach (var response in responses.ConfigureAwait(false))
            yield return response;
    }

    public async IAsyncEnumerable<ResponsePacket> OnRecipient(RecipientRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var info = new SenderRecipientInformation(request.Recipient, request.ESmtpArguments);
        var context = _context ??= contextFactory();

        var commitAction = await context.OnRecipient(info, cancellationToken).ConfigureAwait(false);
        var responses = commitAction.CreateResponsePackets().ConfigureAwait(false).WithCancellation(cancellationToken);

        await foreach (var response in responses.ConfigureAwait(false))
            yield return response;
    }

    public async IAsyncEnumerable<ResponsePacket> OnHeader(HeaderRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var info = new HeaderInformation(request.Name, request.Value);
        var context = _context ??= contextFactory();

        var commitAction = await context.OnHeader(info, cancellationToken).ConfigureAwait(false);
        var responses = commitAction.CreateResponsePackets().ConfigureAwait(false).WithCancellation(cancellationToken);

        await foreach (var response in responses.ConfigureAwait(false))
            yield return response;
    }

    public async IAsyncEnumerable<ResponsePacket> OnEndOfHeaders(EndOfHeadersRequestPacket _, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var context = _context ??= contextFactory();

        var commitAction = await context.OnEndOfHeaders(cancellationToken).ConfigureAwait(false);
        var responses = commitAction.CreateResponsePackets().ConfigureAwait(false).WithCancellation(cancellationToken);

        await foreach (var response in responses.ConfigureAwait(false))
            yield return response;
    }

    public async IAsyncEnumerable<ResponsePacket> OnBodyChunk(BodyChunkRequestPacket request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var context = _context ??= contextFactory();

        var commitAction = await context.OnBodyChunk(request.DataSpan, cancellationToken).ConfigureAwait(false);
        var responses = commitAction.CreateResponsePackets().ConfigureAwait(false).WithCancellation(cancellationToken);

        await foreach (var response in responses.ConfigureAwait(false))
            yield return response;
    }

    public IAsyncEnumerable<ResponsePacket> OnEndOfBody(EndOfBodyRequestPacket request, CancellationToken cancellationToken)
    {
        var context = _context ??= contextFactory();

        var actions = context.OnEndOfBody();
        var responses = actions.SelectMany(action => action.CreateResponsePackets());

        return responses;
    }
}