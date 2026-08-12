using System;
using System.Collections.Generic;
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

    public IAsyncEnumerable<ResponsePacket> OnDefineMacro(DefineMacroRequestPacket request)
    {
        return Core(CancellationToken.None);

        async IAsyncEnumerable<ResponsePacket> Core([EnumeratorCancellation] CancellationToken cancellationToken)
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
    }

    public IAsyncEnumerable<ResponsePacket> OnConnectionEstablished(ConnectRequestPacket request)
    {
        return Invoke(
            new ConnectionInformation(request.HostName, request.EndPoint),
            static (context, info, token) => context.OnConnectionEstablished(info, token));
    }

    public IAsyncEnumerable<ResponsePacket> OnClientHello(HeloRequestPacket request)
    {
        return Invoke(
            new ClientHelloInformation(request.HeloString),
            static (context, info, token) => context.OnClientHello(info, token));
    }

    public IAsyncEnumerable<ResponsePacket> OnSender(SenderRequestPacket request)
    {
        return Invoke(
            new SenderRecipientInformation(request.Sender, request.ESmtpArguments),
            static (context, info, token) => context.OnSender(info, token));
    }

    public IAsyncEnumerable<ResponsePacket> OnRecipient(RecipientRequestPacket request)
    {
        return Invoke(
            new SenderRecipientInformation(request.Recipient, request.ESmtpArguments),
            static (context, info, token) => context.OnRecipient(info, token));
    }

    public IAsyncEnumerable<ResponsePacket> OnHeader(HeaderRequestPacket request)
    {
        return Invoke(
            new HeaderInformation(request.Name, request.Value),
            static (context, info, token) => context.OnHeader(info, token));
    }

    public IAsyncEnumerable<ResponsePacket> OnEndOfHeaders(EndOfHeadersRequestPacket request)
    {
        return Invoke(
            request,
            static (context, _, token) => context.OnEndOfHeaders(token));
    }

    // The body arrives as a span, which cannot be a type argument, so this one hands the packet through
    // and reads the span inside the callback instead.
    public IAsyncEnumerable<ResponsePacket> OnBodyChunk(BodyChunkRequestPacket request)
    {
        return Invoke(
            request,
            static (context, packet, token) => context.OnBodyChunk(packet.DataSpan, token));
    }

    public IAsyncEnumerable<ResponsePacket> OnEndOfBody(EndOfBodyRequestPacket _)
    {
        var context = _context ??= contextFactory();

        return EnumerateResponses(context.OnEndOfBody());

        // End-of-body is the one callback that may answer with more than a single decision, and the MTA
        // reads that stream as modifications first and exactly one verdict last. Letting a wrong sequence
        // through would leave the MTA waiting for a verdict that never comes, or acting on a modification
        // it has already been told to ignore - both far harder to place than a throw here.
        static async IAsyncEnumerable<ResponsePacket> EnumerateResponses(
            IAsyncEnumerable<Action> actions,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var isCommitted = false;

            await foreach (var action in actions.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (isCommitted)
                    throw new InvalidOperationException($"{nameof(IMilterCallbacks.OnEndOfBody)} yielded an action after the commit action.");

                isCommitted = action is CommitAction;

                await foreach (var response in action.CreateResponsePackets().WithCancellation(cancellationToken).ConfigureAwait(false))
                    yield return response;
            }

            if (!isCommitted)
                throw new InvalidOperationException($"{nameof(IMilterCallbacks.OnEndOfBody)} must yield exactly one commit action.");
        }
    }

    private async IAsyncEnumerable<ResponsePacket> Invoke<TInfo>(
        TInfo info,
        Func<IMilterCallbacks, TInfo, CancellationToken, ValueTask<CommitAction>> callback,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var context = _context ??= contextFactory();

        var commitAction = await callback(context, info, cancellationToken).ConfigureAwait(false);

        await foreach (var response in commitAction.CreateResponsePackets().WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return response;
    }
}