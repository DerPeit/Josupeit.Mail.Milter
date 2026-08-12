// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

/// <summary>
/// The point in the SMTP conversation that a set of macro definitions belongs to.
/// </summary>
/// <remarks>
/// The MTA sends macros ahead of the event they describe, and which macros it sends depends on where in
/// the conversation it is. This says which event the values in a
/// <see cref="MacroDefinitionInformation"/> were sent for.
/// </remarks>
public enum ProtocolStage
{
    /// <summary>
    /// A client connected.
    /// </summary>
    Connect,

    /// <summary>
    /// The client sent <c>HELO</c> or <c>EHLO</c>.
    /// </summary>
    ClientHello,

    /// <summary>
    /// The client sent <c>MAIL FROM</c>.
    /// </summary>
    Sender,

    /// <summary>
    /// The client sent <c>RCPT TO</c>.
    /// </summary>
    Recipient,

    /// <summary>
    /// A message header arrived.
    /// </summary>
    Header,

    /// <summary>
    /// The header block ended.
    /// </summary>
    EndOfHeaders,

    /// <summary>
    /// A chunk of the message body arrived.
    /// </summary>
    Body,

    /// <summary>
    /// The message is complete.
    /// </summary>
    EndOfBody,
}