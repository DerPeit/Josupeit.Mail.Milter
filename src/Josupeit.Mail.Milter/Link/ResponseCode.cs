// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Link;

// These values map to the SMFIR_ constants; the negotiation reply is the exception and reuses SMFIC_OPTNEG.
public enum ResponseCode : byte
{
    Unspecified = 0,
    OptionNegotiation = (byte)'O', // SMFIC_OPTNEG Option negotiation (in response to SMFIC_OPTNEG)

    // The following are modification actions
    AddRecipient = (byte)'+', // SMFIR_ADDRCPT
    DeleteRecipient = (byte)'-', // SMFIR_DELRCPT
    AddHeader = (byte)'h', // SMFIR_ADDHEADER
    ChangeOrDeleteHeader = (byte)'m', // SMFIR_CHGHEADER
    ReplaceBody = (byte)'b', // SMFIR_REPLBODY

    // The following are accept/reject actions
    ContinueProcessing = (byte)'c', // SMFIR_CONTINUE
    AcceptMessage = (byte)'a', // SMFIR_ACCEPT
    DiscardMessage = (byte)'d', // SMFIR_DISCARD
    QuarantineMessage = (byte)'q', // SMFIR_QUARANTINE
    RejectMessage = (byte)'r', // SMFIR_REJECT
    DeferMessage = (byte)'t', // SMFIR_TEMPFAIL
    ReplyWithCode = (byte)'y', // SMFIR_REPLYCODE

    // The following is an asynchronous action
    Progress = (byte)'p', // SMFIR_PROGRESS
}