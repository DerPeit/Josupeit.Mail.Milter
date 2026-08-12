// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// The one-byte code that opens every frame the filter sends back.
/// </summary>
/// <remarks>
/// These map to the <c>SMFIR_</c> constants. The negotiation reply is the exception: it has no
/// <c>SMFIR_</c> of its own and reuses <c>SMFIC_OPTNEG</c>, the code of the request it answers.
/// </remarks>
public enum ResponseCode : byte
{
    /// <summary>
    /// No code. Not a value the protocol defines; it marks a frame whose code has not been set.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The filter's half of option negotiation, in reply to <see cref="CommandCode.OptionNegotiation"/>
    /// (<c>SMFIC_OPTNEG</c>).
    /// </summary>
    OptionNegotiation = (byte)'O',

    /// <summary>
    /// Adds an envelope recipient (<c>SMFIR_ADDRCPT</c>).
    /// </summary>
    AddRecipient = (byte)'+',

    /// <summary>
    /// Removes an envelope recipient (<c>SMFIR_DELRCPT</c>).
    /// </summary>
    DeleteRecipient = (byte)'-',

    /// <summary>
    /// Appends a header (<c>SMFIR_ADDHEADER</c>).
    /// </summary>
    AddHeader = (byte)'h',

    /// <summary>
    /// Changes a header, or removes it by changing it to an empty value (<c>SMFIR_CHGHEADER</c>).
    /// </summary>
    ChangeOrDeleteHeader = (byte)'m',

    /// <summary>
    /// Replaces a chunk of the message body (<c>SMFIR_REPLBODY</c>).
    /// </summary>
    ReplaceBody = (byte)'b',

    /// <summary>
    /// Decides nothing and lets the MTA carry on (<c>SMFIR_CONTINUE</c>).
    /// </summary>
    ContinueProcessing = (byte)'c',

    /// <summary>
    /// Accepts the message and asks for no further events about it (<c>SMFIR_ACCEPT</c>).
    /// </summary>
    AcceptMessage = (byte)'a',

    /// <summary>
    /// Accepts the message on the wire and drops it silently (<c>SMFIR_DISCARD</c>).
    /// </summary>
    DiscardMessage = (byte)'d',

    /// <summary>
    /// Holds the message in the MTA's queue (<c>SMFIR_QUARANTINE</c>).
    /// </summary>
    QuarantineMessage = (byte)'q',

    /// <summary>
    /// Rejects the message permanently (<c>SMFIR_REJECT</c>).
    /// </summary>
    RejectMessage = (byte)'r',

    /// <summary>
    /// Rejects the message temporarily, so the sender retries (<c>SMFIR_TEMPFAIL</c>).
    /// </summary>
    DeferMessage = (byte)'t',

    /// <summary>
    /// Rejects the message with an SMTP code and text the filter chose (<c>SMFIR_REPLYCODE</c>).
    /// </summary>
    ReplyWithCode = (byte)'y',

    /// <summary>
    /// Asks the MTA to restart its reply timeout (<c>SMFIR_PROGRESS</c>).
    /// </summary>
    /// <remarks>
    /// The one asynchronous response: it answers nothing, so it can be sent as often as needed while the
    /// real answer is still being worked out.
    /// </remarks>
    Progress = (byte)'p',
}
