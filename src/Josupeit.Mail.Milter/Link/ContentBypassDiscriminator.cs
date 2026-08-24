// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// The wire form of the events a filter asks the MTA <em>not</em> to send.
/// </summary>
/// <remarks>
/// These map to the <c>SMFIP_</c> constants. Note the inversion:
/// <see cref="IMilterContext.ProtocolRequirements"/> names the events a filter wants and this names the
/// ones it does not, so the two are complements of each other and the conversion happens on the way out.
/// End-of-body has no flag here at all - it cannot be skipped.
/// </remarks>
[Flags]
public enum ContentBypassDiscriminator : uint
{
    /// <summary>
    /// Nothing is skipped; the filter wants every event.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Do not send connection information (<c>SMFIP_NOCONNECT</c>).
    /// </summary>
    NoConnect = 0x01,

    /// <summary>
    /// Do not send the <c>HELO</c> or <c>EHLO</c> name (<c>SMFIP_NOHELO</c>).
    /// </summary>
    NoHelo = 0x02,

    /// <summary>
    /// Do not send <c>MAIL FROM</c> (<c>SMFIP_NOMAIL</c>).
    /// </summary>
    NoSender = 0x04,

    /// <summary>
    /// Do not send <c>RCPT TO</c> (<c>SMFIP_NORCPT</c>).
    /// </summary>
    NoRecipient = 0x08,

    /// <summary>
    /// Do not send message headers (<c>SMFIP_NOHDRS</c>).
    /// </summary>
    NoHeader = 0x20,

    /// <summary>
    /// Do not send the end-of-headers marker (<c>SMFIP_NOEOH</c>).
    /// </summary>
    NoEndOfHeaders = 0x40,

    /// <summary>
    /// Do not send the message body (<c>SMFIP_NOBODY</c>).
    /// </summary>
    /// <remarks>
    /// The one that saves real work: without it the entire message crosses the socket, chunk by chunk,
    /// whether or not the filter looks at any of it.
    /// </remarks>
    NoBody = 0x10,

    /// <summary>
    /// Do not send SMTP commands the MTA does not recognise (<c>SMFIP_NOUNKNOWN</c>).
    /// </summary>
    /// <remarks>
    /// Protocol version 3 and above.
    /// </remarks>
    NoUnknownCommand = 0x100,

    /// <summary>
    /// Do not send the SMTP <c>DATA</c> verb (<c>SMFIP_NODATA</c>).
    /// </summary>
    /// <remarks>
    /// Protocol version 4 and above.
    /// </remarks>
    NoMessageData = 0x200,

    /// <summary>
    /// The filter may answer a repeatable event with <see cref="ResponseCode.Skip"/>
    /// (<c>SMFIP_SKIP</c>).
    /// </summary>
    /// <remarks>
    /// Protocol version 6 and above. Positive rather than a skip, despite living in this enum: it asks
    /// the MTA for a capability instead of asking it to send less.
    /// </remarks>
    SkipRemainingEvents = 0x400,

    /// <summary>
    /// Send recipients the MTA has already rejected (<c>SMFIP_RCPT_REJ</c>).
    /// </summary>
    /// <remarks>
    /// Protocol version 6 and above. Positive: it asks for <em>more</em> events, not fewer.
    /// </remarks>
    RejectedRecipients = 0x800,

    /// <summary>
    /// Keep the space that follows the colon in a header value (<c>SMFIP_HDR_LEADSPC</c>).
    /// </summary>
    /// <remarks>
    /// Protocol version 6 and above. Positive, and it cuts both ways: the value delivered to the filter
    /// keeps its leading space, and the MTA stops inserting one in front of values the filter writes.
    /// </remarks>
    LeadingHeaderSpace = 0x100000,

    /// <summary>
    /// Do not wait for a reply to a message header (<c>SMFIP_NR_HDR</c>).
    /// </summary>
    /// <remarks>
    /// Protocol version 6 and above, and the one member of the no-reply family whose bit sits inside the
    /// version 2 range rather than above it.
    /// </remarks>
    NoReplyToHeader = 0x80,

    /// <summary>
    /// Do not wait for a reply to connection information (<c>SMFIP_NR_CONN</c>).
    /// </summary>
    NoReplyToConnect = 0x1000,

    /// <summary>
    /// Do not wait for a reply to <c>HELO</c> or <c>EHLO</c> (<c>SMFIP_NR_HELO</c>).
    /// </summary>
    NoReplyToClientHello = 0x2000,

    /// <summary>
    /// Do not wait for a reply to <c>MAIL FROM</c> (<c>SMFIP_NR_MAIL</c>).
    /// </summary>
    NoReplyToSender = 0x4000,

    /// <summary>
    /// Do not wait for a reply to <c>RCPT TO</c> (<c>SMFIP_NR_RCPT</c>).
    /// </summary>
    NoReplyToRecipient = 0x8000,

    /// <summary>
    /// Do not wait for a reply to the <c>DATA</c> verb (<c>SMFIP_NR_DATA</c>).
    /// </summary>
    NoReplyToMessageData = 0x10000,

    /// <summary>
    /// Do not wait for a reply to an unrecognised SMTP command (<c>SMFIP_NR_UNKN</c>).
    /// </summary>
    NoReplyToUnknownCommand = 0x20000,

    /// <summary>
    /// Do not wait for a reply to the end-of-headers marker (<c>SMFIP_NR_EOH</c>).
    /// </summary>
    NoReplyToEndOfHeaders = 0x40000,

    /// <summary>
    /// Do not wait for a reply to a body chunk (<c>SMFIP_NR_BODY</c>).
    /// </summary>
    NoReplyToBody = 0x80000,

    /// <summary>
    /// Every skippable event at once, leaving only end-of-body.
    /// </summary>
    /// <remarks>
    /// Skips only. The no-reply family, <see cref="SkipRemainingEvents"/>,
    /// <see cref="RejectedRecipients"/> and <see cref="LeadingHeaderSpace"/> are all deliberately
    /// outside it: they ask for different behaviour rather than for fewer events, and folding them in
    /// would quietly turn "send me nothing" into "and expect no answer either".
    /// </remarks>
    All = NoConnect | NoHelo | NoSender | NoRecipient | NoBody | NoHeader | NoEndOfHeaders | NoUnknownCommand | NoMessageData,
}
