// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

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

    // The MTA reports a rejected recipient through the macros it sends immediately before the recipient
    // itself, never in the RCPT frame, so the two have to be put back together here rather than by every
    // filter that cares.
    private IReadOnlyDictionary<string, string>? _recipientMacros;

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
                CommandCode.MessageData => ProtocolStage.MessageData,
                CommandCode.Header => ProtocolStage.Header,
                CommandCode.EndOfHeaders => ProtocolStage.EndOfHeaders,
                CommandCode.BodyChunk => ProtocolStage.Body,
                CommandCode.EndOfBody => ProtocolStage.EndOfBody,
                _ => (ProtocolStage)(-1),
            };

            if (!Enum.IsDefined(protocolStage))
                yield break;

            if (protocolStage == ProtocolStage.Recipient)
                _recipientMacros = request.Macros;

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
            new SenderInformation(request.Sender, request.ESmtpArguments),
            static (context, info, token) => context.OnSender(info, token));
    }

    public IAsyncEnumerable<ResponsePacket> OnRecipient(RecipientRequestPacket request)
    {
        var isRejected = IsRejectedByMta(_recipientMacros);

        // Cleared as soon as it has been read. A second RCPT TO can arrive without macros of its own,
        // and it would otherwise inherit the verdict passed on the first one.
        _recipientMacros = null;

        return Invoke(
            new RecipientInformation(request.Recipient, request.ESmtpArguments, isRejected),
            static (context, info, token) => context.OnRecipient(info, token));
    }

    // Postfix sets {rcpt_mailer} to "error" for a recipient it has already refused, and puts the status
    // code and reason in {rcpt_host} and {rcpt_addr}. The braces are part of the macro name as it
    // arrives on the wire, so looking it up without them silently never matches.
    private static bool IsRejectedByMta(IReadOnlyDictionary<string, string>? macros)
    {
        return macros != null
               && macros.TryGetValue("{rcpt_mailer}", out var mailer)
               && string.Equals(mailer, "error", StringComparison.Ordinal);
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

    public IAsyncEnumerable<ResponsePacket> OnMessageDataStarted(MessageDataRequestPacket request)
    {
        return Invoke(
            request,
            static (context, _, token) => context.OnMessageDataStarted(token));
    }

    public IAsyncEnumerable<ResponsePacket> OnUnknownCommand(UnknownCommandRequestPacket request)
    {
        return Invoke(
            new UnknownCommandInformation(request.CommandText),
            static (context, info, token) => context.OnUnknownCommand(info, token));
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