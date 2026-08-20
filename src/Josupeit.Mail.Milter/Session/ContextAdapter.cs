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

internal sealed class ContextAdapter(Func<IMilterCallbacks> contextFactory, ProtocolNegotiationOutcome negotiation) : IAsyncDisposable
{
    private IMilterCallbacks? _context;

    // The MTA reports a rejected recipient through the macros it sends immediately before the recipient
    // itself, never in the RCPT frame, so the two have to be put back together here rather than by every
    // filter that cares.
    private IReadOnlyDictionary<string, string>? _recipientMacros;

    // Set once the filter has answered SkipRemainingEvents to one of the three repeatable events. A
    // conforming MTA stops sending them, but the unit tests exist for what a correct MTA can never
    // produce, and a stray one must not reach a callback that asked to be left alone.
    private bool _isSkippingRecipients;
    private bool _isSkippingHeaders;
    private bool _isSkippingBody;

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
            static (context, info, token) => context.OnConnectionEstablished(info, token),
            negotiation.IsAnsweringConnection);
    }

    public IAsyncEnumerable<ResponsePacket> OnClientHello(HeloRequestPacket request)
    {
        return Invoke(
            new ClientHelloInformation(request.HeloString),
            static (context, info, token) => context.OnClientHello(info, token),
            negotiation.IsAnsweringClientHello);
    }

    public IAsyncEnumerable<ResponsePacket> OnSender(SenderRequestPacket request)
    {
        return Invoke(
            new SenderInformation(request.Sender, request.ESmtpArguments),
            static (context, info, token) => context.OnSender(info, token),
            negotiation.IsAnsweringSender);
    }

    public IAsyncEnumerable<ResponsePacket> OnRecipient(RecipientRequestPacket request)
    {
        var isRejected = IsRejectedByMta(_recipientMacros);

        // Cleared as soon as it has been read. A second RCPT TO can arrive without macros of its own,
        // and it would otherwise inherit the verdict passed on the first one.
        _recipientMacros = null;

        return InvokeRepeatable(
            new RecipientInformation(request.Recipient, request.ESmtpArguments, isRejected),
            static (context, info, token) => context.OnRecipient(info, token),
            negotiation.IsAnsweringRecipients,
            _isSkippingRecipients,
            static adapter => adapter._isSkippingRecipients = true);
    }

    public IAsyncEnumerable<ResponsePacket> OnHeader(HeaderRequestPacket request)
    {
        return InvokeRepeatable(
            new HeaderInformation(request.Name, request.Value),
            static (context, info, token) => context.OnHeader(info, token),
            negotiation.IsAnsweringHeaders,
            _isSkippingHeaders,
            static adapter => adapter._isSkippingHeaders = true);
    }

    public IAsyncEnumerable<ResponsePacket> OnEndOfHeaders(EndOfHeadersRequestPacket request)
    {
        return Invoke(
            request,
            static (context, _, token) => context.OnEndOfHeaders(token),
            negotiation.IsAnsweringEndOfHeaders);
    }

    public IAsyncEnumerable<ResponsePacket> OnMessageDataStarted(MessageDataRequestPacket request)
    {
        return Invoke(
            request,
            static (context, _, token) => context.OnMessageDataStarted(token),
            negotiation.IsAnsweringMessageData);
    }

    public IAsyncEnumerable<ResponsePacket> OnUnknownCommand(UnknownCommandRequestPacket request)
    {
        return Invoke(
            new UnknownCommandInformation(request.CommandText),
            static (context, info, token) => context.OnUnknownCommand(info, token),
            negotiation.IsAnsweringUnknownCommands);
    }

    // The body arrives as a span, which cannot be a type argument, so this one hands the packet through
    // and reads the span inside the callback instead.
    public IAsyncEnumerable<ResponsePacket> OnBodyChunk(BodyChunkRequestPacket request)
    {
        return InvokeRepeatable(
            request,
            static (context, packet, token) => context.OnBodyChunk(packet.DataSpan, token),
            negotiation.IsAnsweringBody,
            _isSkippingBody,
            static adapter => adapter._isSkippingBody = true);
    }

    public IAsyncEnumerable<ResponsePacket> OnEndOfBody(EndOfBodyRequestPacket _)
    {
        var context = _context ??= contextFactory();

        return EnumerateResponses(context.OnEndOfBody());

        // End-of-body is the one callback that may answer with more than a single decision, and the MTA
        // reads that stream as modifications first and exactly one verdict last. Letting a wrong sequence
        // through would leave the MTA waiting for a verdict that never comes, or acting on a modification
        // it has already been told to ignore - both far harder to place than a throw here.
        async IAsyncEnumerable<ResponsePacket> EnumerateResponses(
            IAsyncEnumerable<Action> actions,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var isCommitted = false;

            await foreach (var action in actions.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (isCommitted)
                    throw new InvalidOperationException($"{nameof(IMilterCallbacks.OnEndOfBody)} yielded an action after the commit action.");

                // End-of-body happens once per message, so there is nothing left to stop sending.
                if (action is SkipRemainingEventsAction)
                    throw new InvalidOperationException(
                        $"{nameof(IMilterCallbacks.OnEndOfBody)} yielded SkipRemainingEvents, which only "
                        + "answers an event that repeats.");

                isCommitted = action is CommitAction;

                await foreach (var response in action.CreateResponsePackets().WithCancellation(cancellationToken).ConfigureAwait(false))
                    yield return response;
            }

            if (!isCommitted)
                throw new InvalidOperationException($"{nameof(IMilterCallbacks.OnEndOfBody)} must yield exactly one commit action.");

            // The message is over. The adapter is not: it is disposed on ABORT and QUIT only, so an MTA
            // that starts the next message without either would otherwise carry this message's skips
            // into it and silently starve the filter of events.
            _isSkippingRecipients = false;
            _isSkippingHeaders = false;
            _isSkippingBody = false;
        }
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

    // There is nothing left to stop sending when the event happens once, so a skip answered there means
    // something the protocol cannot express. Unlike a skip the MTA declined, this is the filter's own
    // mistake and is worth saying out loud.
    private static void ThrowIfNotRepeatable(CommitAction commitAction)
    {
        if (commitAction is SkipRemainingEventsAction)
            throw new InvalidOperationException(
                "SkipRemainingEvents was answered from an event that happens once. Only headers, "
                + "recipients and body chunks repeat, so only they can be skipped.");
    }

    // The MTA is not reading a reply to this event, so there is nowhere to put a decision. Writing one
    // anyway would be read as the answer to the next command and leave every request and response after
    // it out of step for the rest of the connection; dropping it silently would deliver mail the filter
    // rejected. Both are worse than ending the session.
    //
    // Unreachable when the flag was derived, because the analysis only sets it once it has proved the
    // callback cannot answer anything else - which makes this a check on the analysis as much as on a
    // hand-written declaration.
    private static void ThrowIfDecisionCannotBeDelivered(CommitAction commitAction)
    {
        if (commitAction is not ContinueAction)
            throw new InvalidOperationException(
                "A callback listed in NoReplyRequirements answered with something other than Continue. "
                + "The MTA is not waiting for a reply to that event, so the decision cannot be delivered.");
    }

    // The three events that repeat, and the only ones a skip means anything for.
    //
    // Two rules that differ on purpose. Skipping is an optimisation, so a skip the MTA never granted
    // becomes a Continue and the message is unaffected; throwing over a saving the peer declined would
    // drop mail for nothing. Answering a skip from an event that happens once is a different matter -
    // there is nothing to skip, so the filter meant something the protocol cannot express, and that is a
    // programming error rather than a peer's shortfall.
    private async IAsyncEnumerable<ResponsePacket> InvokeRepeatable<TInfo>(
        TInfo info,
        Func<IMilterCallbacks, TInfo, CancellationToken, ValueTask<CommitAction>> callback,
        bool isAnswered,
        bool isSkipping,
        System.Action<ContextAdapter> markSkipping,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // The filter asked not to hear about these again. Invoking the callback anyway would break the
        // promise the skip just made, so the event is answered without it.
        if (isSkipping)
        {
            if (isAnswered)
                yield return ContinueProcessingResponsePacket.Instance;

            yield break;
        }

        var context = _context ??= contextFactory();

        var commitAction = await callback(context, info, cancellationToken).ConfigureAwait(false);

        if (commitAction is SkipRemainingEventsAction)
        {
            if (!negotiation.IsSkipGranted)
                commitAction = CommitActions.Continue;
            else
                markSkipping(this);
        }

        if (!isAnswered)
        {
            ThrowIfDecisionCannotBeDelivered(commitAction);

            yield break;
        }

        await foreach (var response in commitAction.CreateResponsePackets().WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return response;
    }

    private async IAsyncEnumerable<ResponsePacket> Invoke<TInfo>(
        TInfo info,
        Func<IMilterCallbacks, TInfo, CancellationToken, ValueTask<CommitAction>> callback,
        bool isAnswered,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var context = _context ??= contextFactory();

        var commitAction = await callback(context, info, cancellationToken).ConfigureAwait(false);

        ThrowIfNotRepeatable(commitAction);

        if (isAnswered)
        {
            await foreach (var response in commitAction.CreateResponsePackets().WithCancellation(cancellationToken).ConfigureAwait(false))
                yield return response;

            yield break;
        }

        ThrowIfDecisionCannotBeDelivered(commitAction);
    }
}
