// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The events the MTA need not wait for an answer to, declared to it when the connection is negotiated.
/// </summary>
/// <remarks>
/// <para>
/// A round trip per event is the cost of being able to decide at that event. A callback that never
/// decides anything - one that only collects what it will need later - pays it for nothing, and this is
/// how that is reclaimed. Needs an MTA speaking protocol version 6.
/// </para>
/// <para>
/// The source generator derives this by proving that a callback can only ever answer
/// <see cref="CommitActions.Continue"/>. A callback it cannot prove keeps its reply, so the failure
/// direction is the harmless one and a filter rarely needs to declare this by hand.
/// </para>
/// <para>
/// Declaring it by hand is a promise, and breaking it is not a small mistake. An event listed here must
/// be answered with <see cref="CommitActions.Continue"/>; anything else throws, because writing a reply
/// the MTA is not reading would be taken as the answer to the <em>next</em> command and put every
/// request and response after it out of step for the rest of the connection.
/// </para>
/// </remarks>
[Flags]
[NoReplyRequirementsMarker(MinimumProtocolVersion = 6)]
public enum NoReplyRequirements
{
    /// <summary>
    /// Every event is answered.
    /// </summary>
    None = 0,

    /// <summary>
    /// The MTA need not wait for an answer to a connection (<c>SMFIP_NR_CONN</c>).
    /// </summary>
    Connect = 1,

    /// <summary>
    /// The MTA need not wait for an answer to <c>HELO</c> or <c>EHLO</c> (<c>SMFIP_NR_HELO</c>).
    /// </summary>
    ClientHello = 2,

    /// <summary>
    /// The MTA need not wait for an answer to <c>MAIL FROM</c> (<c>SMFIP_NR_MAIL</c>).
    /// </summary>
    Sender = 4,

    /// <summary>
    /// The MTA need not wait for an answer to <c>RCPT TO</c> (<c>SMFIP_NR_RCPT</c>).
    /// </summary>
    Recipient = 8,

    /// <summary>
    /// The MTA need not wait for an answer to a message header (<c>SMFIP_NR_HDR</c>).
    /// </summary>
    /// <remarks>
    /// Usually the one worth having: a filter that reads headers rather than judging them pays one round
    /// trip per header without it.
    /// </remarks>
    Header = 16,

    /// <summary>
    /// The MTA need not wait for an answer to the end of the header block (<c>SMFIP_NR_EOH</c>).
    /// </summary>
    EndOfHeaders = 32,

    /// <summary>
    /// The MTA need not wait for an answer to a body chunk (<c>SMFIP_NR_BODY</c>).
    /// </summary>
    Body = 64,

    /// <summary>
    /// The MTA need not wait for an answer to the <c>DATA</c> verb (<c>SMFIP_NR_DATA</c>).
    /// </summary>
    MessageData = 128,

    /// <summary>
    /// The MTA need not wait for an answer to an unrecognised SMTP command (<c>SMFIP_NR_UNKN</c>).
    /// </summary>
    UnknownCommand = 256,

    /// <summary>
    /// Every event the protocol allows to go unanswered.
    /// </summary>
    /// <remarks>
    /// End-of-body is deliberately absent, and has no member here at all: it is where the filter gives
    /// its verdict on the message, so the MTA always waits for it.
    /// </remarks>
    All = Connect | ClientHello | Sender | Recipient | Header | EndOfHeaders | Body | MessageData | UnknownCommand,
}
