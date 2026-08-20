// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// The one-byte code that opens every frame the MTA sends.
/// </summary>
/// <remarks>
/// These map to the <c>SMFIC_</c> constants.
/// </remarks>
public enum CommandCode : byte
{
    /// <summary>
    /// No code. Not a value the protocol defines; it marks a frame whose code has not been set.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Option negotiation (<c>SMFIC_OPTNEG</c>). Answered with an option negotiation packet.
    /// </summary>
    /// <remarks>
    /// The first thing on every connection. Version, the modifications the filter intends to make and the
    /// events it wants skipped are all settled here, and nothing else may precede it.
    /// </remarks>
    OptionNegotiation = (byte)'O',

    /// <summary>
    /// Macro definitions (<c>SMFIC_MACRO</c>). Answered with nothing at all.
    /// </summary>
    /// <remarks>
    /// The only command that expects no reply. Sending one anyway puts every request and response after it
    /// out of step for the rest of the connection.
    /// </remarks>
    Macro = (byte)'D',

    /// <summary>
    /// SMTP connection information (<c>SMFIC_CONNECT</c>). Answered with an accept/reject action.
    /// </summary>
    Connect = (byte)'C',

    /// <summary>
    /// The <c>HELO</c> or <c>EHLO</c> name (<c>SMFIC_HELO</c>). Answered with an accept/reject action.
    /// </summary>
    Helo = (byte)'H',

    /// <summary>
    /// <c>MAIL FROM</c> information (<c>SMFIC_MAIL</c>). Answered with an accept/reject action.
    /// </summary>
    Sender = (byte)'M',

    /// <summary>
    /// <c>RCPT TO</c> information (<c>SMFIC_RCPT</c>). Answered with an accept/reject action.
    /// </summary>
    Recipient = (byte)'R',

    /// <summary>
    /// One message header (<c>SMFIC_HEADER</c>). Answered with an accept/reject action.
    /// </summary>
    Header = (byte)'L',

    /// <summary>
    /// The end-of-headers marker (<c>SMFIC_EOH</c>). Answered with an accept/reject action.
    /// </summary>
    EndOfHeaders = (byte)'N',

    /// <summary>
    /// A chunk of the message body (<c>SMFIC_BODY</c>). Answered with an accept/reject action.
    /// </summary>
    BodyChunk = (byte)'B',

    /// <summary>
    /// The end-of-body marker (<c>SMFIC_BODYEOB</c>). Answered with zero or more modification actions and
    /// then an accept/reject action.
    /// </summary>
    EndOfBody = (byte)'E',

    /// <summary>
    /// Ends the milter conversation (<c>SMFIC_QUIT</c>). Answered by closing the connection.
    /// </summary>
    Quit = (byte)'Q',

    /// <summary>
    /// Abandons the current message (<c>SMFIC_ABORT</c>). Answered with nothing at all.
    /// </summary>
    /// <remarks>
    /// Resets the filter to where it was before <see cref="Helo"/> but leaves the connection open, so the
    /// next message arrives on it without the connection being described again.
    /// </remarks>
    Abort = (byte)'A',

    /// <summary>
    /// The SMTP <c>DATA</c> verb (<c>SMFIC_DATA</c>). Answered with an accept/reject action.
    /// </summary>
    /// <remarks>
    /// Protocol version 4 and above. The last point before the message content begins, and the first at
    /// which every envelope recipient has been seen.
    /// </remarks>
    MessageData = (byte)'T',

    /// <summary>
    /// An SMTP command the MTA does not recognise (<c>SMFIC_UNKNOWN</c>). Answered with an
    /// accept/reject action.
    /// </summary>
    /// <remarks>
    /// Protocol version 3 and above. Carries the command line as the client sent it, which is what makes
    /// it useful for spotting a client probing for verbs the MTA does not implement.
    /// </remarks>
    UnknownCommand = (byte)'U',

    /// <summary>
    /// Ends the SMTP connection but keeps the milter connection (<c>SMFIC_QUIT_NC</c>). Answered with
    /// nothing at all.
    /// </summary>
    /// <remarks>
    /// Protocol version 6 and above. A new client follows on the same milter connection, so everything
    /// the filter was told about the old one - its address above all - stops being true.
    /// </remarks>
    QuitForNewConnection = (byte)'K',
}
