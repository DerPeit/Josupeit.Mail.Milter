// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.IO;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Builds the actions that change a message. This is the only way to obtain one.
/// </summary>
/// <remarks>
/// <para>
/// Every string that leaves for the MTA is validated here, and the action records have internal
/// constructors and get-only properties so there is no route around it - not even a
/// <see langword="with"/> expression. Control characters are rejected everywhere, and header names must
/// be RFC 5322 <c>ftext</c>: printable ASCII other than the colon. That is what stops a value taken from
/// the message under inspection from splicing a second header, or an entire extra reply, into the
/// outgoing one.
/// </para>
/// <para>
/// Every action here also needs the matching <see cref="ActionRequirements"/> flag on the context. The
/// MTA is told during negotiation what the filter intends to do, and refuses anything it was not warned
/// about.
/// </para>
/// </remarks>
public static class ModificationActions
{
    /// <summary>
    /// Adds an envelope recipient to the message.
    /// </summary>
    /// <param name="recipient">The address to add to the envelope.</param>
    /// <returns>The action to yield from <see cref="IMilterCallbacks.OnEndOfBody"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="recipient"/> is empty or contains a control character.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="recipient"/> is
    /// <see langword="null"/>.</exception>
    public static AddRecipientAction AddRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);
        ArgumentException.ThrowIfContainsControlCharacter(recipient);

        return new AddRecipientAction(recipient);
    }

    /// <summary>
    /// Removes an envelope recipient from the message.
    /// </summary>
    /// <param name="recipient">
    /// The address to remove, spelled as the MTA spelled it in <see cref="IMilterCallbacks.OnRecipient"/>.
    /// </param>
    /// <returns>The action to yield from <see cref="IMilterCallbacks.OnEndOfBody"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="recipient"/> is empty or contains a control character.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="recipient"/> is
    /// <see langword="null"/>.</exception>
    public static DeleteRecipientAction DeleteRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);
        ArgumentException.ThrowIfContainsControlCharacter(recipient);

        return new DeleteRecipientAction(recipient);
    }

    /// <summary>
    /// Appends a header to the message.
    /// </summary>
    /// <param name="name">The header name, without the separating colon.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The action to yield from <see cref="IMilterCallbacks.OnEndOfBody"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="value"/> is empty, <paramref name="name"/> is not RFC
    /// 5322 <c>ftext</c>, or <paramref name="value"/> contains a control character.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static AddHeaderAction AddHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNotHeaderName(name);
        ArgumentException.ThrowIfContainsControlCharacter(value);

        return new AddHeaderAction(name, value);
    }

    /// <summary>
    /// Replaces the value of a header already on the message.
    /// </summary>
    /// <param name="name">The header name, without the separating colon.</param>
    /// <param name="value">The value to put in its place.</param>
    /// <param name="index">
    /// Which occurrence of <paramref name="name"/> to change, counting from zero. Defaults to the first.
    /// </param>
    /// <returns>The action to yield from <see cref="IMilterCallbacks.OnEndOfBody"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="value"/> is empty, <paramref name="name"/> is not RFC
    /// 5322 <c>ftext</c>, or <paramref name="value"/> contains a control character.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static ChangeHeaderAction ChangeHeader(string name, string value, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentException.ThrowIfNotHeaderName(name);
        ArgumentException.ThrowIfContainsControlCharacter(value);

        return new ChangeHeaderAction(name, value, index);
    }

    /// <summary>
    /// Removes a header from the message.
    /// </summary>
    /// <param name="name">The header name, without the separating colon.</param>
    /// <param name="index">
    /// Which occurrence of <paramref name="name"/> to remove, counting from zero. Defaults to the first.
    /// </param>
    /// <returns>The action to yield from <see cref="IMilterCallbacks.OnEndOfBody"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or is not RFC 5322 <c>ftext</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static DeleteHeaderAction DeleteHeader(string name, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentException.ThrowIfNotHeaderName(name);

        return new DeleteHeaderAction(name, index);
    }

    /// <summary>
    /// Replaces the body of the message with the contents of a stream.
    /// </summary>
    /// <param name="stream">
    /// The replacement body. It is read as the action is sent, which is after the callback yielded it, so
    /// it has to still be readable then.
    /// </param>
    /// <param name="leaveOpen">
    /// <see langword="true"/> to leave <paramref name="stream"/> open once it has been sent;
    /// <see langword="false"/>, the default, to dispose it.
    /// </param>
    /// <returns>The action to yield from <see cref="IMilterCallbacks.OnEndOfBody"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static ReplaceBodyAction ReplaceBody(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new ReplaceBodyAction(stream, leaveOpen);
    }

    /// <summary>
    /// Holds the message in the MTA's queue instead of delivering it.
    /// </summary>
    /// <param name="reason">Why the message is being held, for whoever reviews the queue.</param>
    /// <returns>The action to yield from <see cref="IMilterCallbacks.OnEndOfBody"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is empty or contains a control character.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is <see langword="null"/>.</exception>
    public static QuarantineAction QuarantineMessage(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ArgumentException.ThrowIfContainsControlCharacter(reason);

        return new QuarantineAction(reason);
    }
}
