// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The SMTP events a filter can react to, one method per event.
/// </summary>
/// <remarks>
/// <para>
/// Every method has a default implementation that answers <see cref="CommitActions.Continue"/>, so an
/// implementation overrides only what it cares about and the rest carries on untouched. Which events
/// actually arrive is decided separately, by <see cref="IMilterContext.ProtocolRequirements"/>.
/// </para>
/// <para>
/// The events arrive in the order the SMTP conversation produces them, and each one blocks the MTA until
/// it is answered: a slow callback is a slow mail server. Anything that cannot be answered promptly
/// belongs in <see cref="OnEndOfBody"/>, which can ask for more time with
/// <see cref="ProgressAction.Instance"/>.
/// </para>
/// <para>
/// An exception thrown from any of these ends the session and drops the connection. What the MTA does
/// with the message then is its own configuration - Postfix applies <c>milter_default_action</c>, which
/// defaults to a temporary failure.
/// </para>
/// </remarks>
public interface IMilterCallbacks
{
    /// <summary>
    /// Called when the MTA supplies macro values ahead of an event.
    /// </summary>
    /// <remarks>
    /// This is the one callback that decides nothing: the protocol expects no reply to it, so there is no
    /// action to return. Its purpose is to let a filter keep the values it will need when the event they
    /// belong to arrives.
    /// </remarks>
    /// <param name="info">The macro values and the event they were sent for.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>A task that completes when the values have been taken note of.</returns>
    ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
    {
        return new();
    }

    /// <summary>
    /// Called when a client has connected to the MTA.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.Connect"/>. This is the only place the client's address
    /// is offered, and it arrives once per connection rather than once per message - so a filter that
    /// needs it for a later decision has to keep it.
    /// </remarks>
    /// <param name="info">The hostname and endpoint the MTA has for the client.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the connection.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.Connect))]
    ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called when the client has sent <c>HELO</c> or <c>EHLO</c>.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.ClientHello"/>. May happen more than once on a
    /// connection: a client that starts TLS greets again afterwards.
    /// </remarks>
    /// <param name="info">The name the client announced itself as.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the message.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.ClientHello))]
    ValueTask<CommitAction> OnClientHello(ClientHelloInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called when the client has sent <c>MAIL FROM</c>.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.Sender"/>. This is the envelope sender, which is what
    /// bounces go to; it need not agree with the <c>From</c> header the recipient will see.
    /// </remarks>
    /// <param name="info">The envelope address and any ESMTP parameters that followed it.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the message.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.Sender))]
    ValueTask<CommitAction> OnSender(SenderRecipientInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called once for each <c>RCPT TO</c> the client sends.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.Recipient"/>. Rejecting here refuses that one recipient
    /// and leaves the rest of the message alone, which is what makes per-recipient policy possible.
    /// </remarks>
    /// <param name="info">The envelope address and any ESMTP parameters that followed it.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with this recipient.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.Recipient))]
    ValueTask<CommitAction> OnRecipient(SenderRecipientInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called when the client has sent <c>DATA</c> and the message content is about to follow.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.MessageData"/>, and an MTA speaking protocol version 4
    /// or above. This is the one point at which the entire envelope has been seen and none of the
    /// content has, so a decision that turns only on who is writing to whom can be made here without
    /// the message ever being transferred.
    /// </remarks>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the message.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.MessageData))]
    ValueTask<CommitAction> OnMessageDataStarted(CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called when the client has sent an SMTP command the MTA does not recognise.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.UnknownCommand"/>, and an MTA speaking protocol version
    /// 3 or above. Unlike the other events this one has no fixed place in the conversation - an
    /// unrecognised verb can arrive wherever the client chooses to send it, including before
    /// <c>HELO</c>.
    /// </remarks>
    /// <param name="info">The command line the MTA could not parse.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the message.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.UnknownCommand))]
    ValueTask<CommitAction> OnUnknownCommand(UnknownCommandInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called once for each header of the message.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.Header"/>. The headers are only reported here, never
    /// changed: a filter that wants to rewrite one collects what it needs and yields the modification
    /// from <see cref="OnEndOfBody"/>.
    /// </remarks>
    /// <param name="info">The name and value of the header.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the message.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.Header))]
    ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
    {
        return new (CommitActions.Continue);
    }

    /// <summary>
    /// Called when the header block has ended and the body is about to start.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.EndOfHeaders"/>. The last point at which every header has
    /// been seen but none of the body has, which is where a decision that needs only the headers can be
    /// made without waiting for the rest of the message.
    /// </remarks>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the message.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.EndOfHeaders))]
    ValueTask<CommitAction> OnEndOfHeaders(CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called for each chunk of the message body.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="ProtocolRequirements.Body"/>. The body arrives in pieces, not in one go -
    /// Postfix caps each at 64 KiB - so a filter that needs the whole body has to accumulate it, and one
    /// that scans for a pattern has to cope with the pattern straddling two chunks. The span is only
    /// valid for the duration of the call; copy anything that must outlive it.
    /// </remarks>
    /// <param name="chunk">The bytes of this part of the body.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>What the MTA should do with the message.</returns>
    [MilterCallbackMarker(nameof(ProtocolRequirements.Body))]
    ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    /// <summary>
    /// Called when the message is complete. The only place a message can be modified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Always delivered - <see cref="ProtocolRequirements.EndOfBody"/> cannot be excluded - and the last
    /// word the filter gets on the message.
    /// </para>
    /// <para>
    /// The sequence has a shape the library enforces: zero or more <see cref="ModificationAction"/>s and
    /// <see cref="ProgressAction"/>s, and then <em>exactly one</em> <see cref="CommitAction"/> to end it.
    /// Yielding nothing, omitting the commit action, or yielding anything after it is a programming error
    /// rather than a protocol one, and throws.
    /// </para>
    /// <para>
    /// This callback takes no <see cref="CancellationToken"/>, unlike the others: it returns the sequence
    /// itself rather than a result, and the enumeration is what gets cancelled. Long work here should
    /// yield <see cref="ProgressAction.Instance"/> now and then, or the MTA gives up waiting.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The modifications to apply, followed by the single action deciding the message's fate.
    /// </returns>
    IAsyncEnumerable<Action> OnEndOfBody()
    {
        return new Action[] { CommitActions.Continue }.ToAsyncEnumerable();
    }
}
