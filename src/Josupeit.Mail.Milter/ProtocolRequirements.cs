// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The SMTP events a filter wants delivered, declared to the MTA when the connection is negotiated.
/// </summary>
/// <remarks>
/// The MTA skips whatever is not listed here, so narrowing this is a real saving rather than a
/// formality: a filter that only looks at senders never has the body streamed to it at all. It should
/// match the callbacks actually overridden - an event that is never asked for cannot arrive, however
/// carefully its callback is written.
/// </remarks>
[Flags]
[ProtocolRequirementsMarker]
public enum ProtocolRequirements
{
    /// <summary>
    /// A client connected. Delivered to <see cref="IMilterCallbacks.OnConnectionEstablished"/>.
    /// </summary>
    Connect = 1,

    /// <summary>
    /// <c>HELO</c> or <c>EHLO</c>. Delivered to <see cref="IMilterCallbacks.OnClientHello"/>.
    /// </summary>
    ClientHello = 2,

    /// <summary>
    /// <c>MAIL FROM</c>. Delivered to <see cref="IMilterCallbacks.OnSender"/>.
    /// </summary>
    Sender = 4,

    /// <summary>
    /// <c>RCPT TO</c>, once per recipient. Delivered to <see cref="IMilterCallbacks.OnRecipient"/>.
    /// </summary>
    Recipient = 8,

    /// <summary>
    /// Each message header. Delivered to <see cref="IMilterCallbacks.OnHeader"/>.
    /// </summary>
    Header = 16,

    /// <summary>
    /// The end of the header block. Delivered to <see cref="IMilterCallbacks.OnEndOfHeaders"/>.
    /// </summary>
    EndOfHeaders = 32,

    /// <summary>
    /// The message body, in chunks. Delivered to <see cref="IMilterCallbacks.OnBodyChunk"/>.
    /// </summary>
    Body = 64,

    /// <summary>
    /// The end of the message. Delivered to <see cref="IMilterCallbacks.OnEndOfBody"/>.
    /// </summary>
    /// <remarks>
    /// Zero on purpose. This event cannot be excluded - it is where the filter gets its last word on the
    /// message - so there is no flag to clear and including it changes nothing.
    /// </remarks>
    EndOfBody = 0, // cannot be excluded

    /// <summary>
    /// The SMTP <c>DATA</c> verb. Delivered to <see cref="IMilterCallbacks.OnMessageDataStarted"/>.
    /// </summary>
    /// <remarks>
    /// Needs protocol version 4 or above from the MTA. Fires after the last recipient and before the
    /// first header, which makes it the one point where the whole envelope is known and none of the
    /// content is.
    /// </remarks>
    MessageData = 128,

    /// <summary>
    /// An SMTP command the MTA does not recognise. Delivered to
    /// <see cref="IMilterCallbacks.OnUnknownCommand"/>.
    /// </summary>
    /// <remarks>
    /// Needs protocol version 3 or above from the MTA.
    /// </remarks>
    UnknownCommand = 256,

    /// <summary>
    /// Every event at once.
    /// </summary>
    All = Connect | ClientHello | Sender | Recipient | MessageData | Header | EndOfHeaders | Body | UnknownCommand | EndOfBody,
}