// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Generator.Markers;

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
    [MilterMacroStageMarker]
    Connect,

    /// <summary>
    /// The client sent <c>HELO</c> or <c>EHLO</c>.
    /// </summary>
    [MilterMacroStageMarker]
    ClientHello,

    /// <summary>
    /// The client sent <c>MAIL FROM</c>.
    /// </summary>
    [MilterMacroStageMarker]
    Sender,

    /// <summary>
    /// The client sent <c>RCPT TO</c>.
    /// </summary>
    [MilterMacroStageMarker]
    Recipient,

    /// <summary>
    /// The client sent <c>DATA</c> and the message content is about to follow.
    /// </summary>
    [MilterMacroStageMarker]
    MessageData,

    /// <summary>
    /// A message header arrived.
    /// </summary>
    Header,

    /// <summary>
    /// The header block ended.
    /// </summary>
    [MilterMacroStageMarker]
    EndOfHeaders,

    /// <summary>
    /// A chunk of the message body arrived.
    /// </summary>
    Body,

    /// <summary>
    /// The message is complete.
    /// </summary>
    [MilterMacroStageMarker]
    EndOfBody,
}
