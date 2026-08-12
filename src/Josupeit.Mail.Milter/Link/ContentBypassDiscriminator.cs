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
    /// Every skippable event at once, leaving only end-of-body.
    /// </summary>
    All = NoConnect | NoHelo | NoSender | NoRecipient | NoBody | NoHeader | NoEndOfHeaders,
}
