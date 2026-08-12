// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// The wire form of the modifications a filter announces during negotiation.
/// </summary>
/// <remarks>
/// These map to the <c>SMFIF_</c> constants. This is what
/// <see cref="IMilterContext.ActionRequirements"/> becomes on the way out, and the MTA answers with the
/// subset it is willing to grant - a filter that asks for more than the MTA supports fails negotiation
/// rather than finding out later.
/// </remarks>
[Flags]
public enum ActionDiscriminator : uint
{
    /// <summary>
    /// The filter changes nothing.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// The filter may add envelope recipients (<c>SMFIF_ADDRCPT</c>).
    /// </summary>
    AddRecipients = 0x04,

    /// <summary>
    /// The filter may remove envelope recipients (<c>SMFIF_DELRCPT</c>).
    /// </summary>
    DeleteRecipients = 0x08,

    /// <summary>
    /// The filter may add headers (<c>SMFIF_ADDHDRS</c>).
    /// </summary>
    AddHeaders = 0x01,

    /// <summary>
    /// The filter may change or remove headers (<c>SMFIF_CHGHDRS</c>).
    /// </summary>
    /// <remarks>
    /// One flag covers both, because the protocol expresses a deletion as a change to an empty value.
    /// </remarks>
    ChangeOrDeleteHeaders = 0x10,

    /// <summary>
    /// The filter may replace the message body (<c>SMFIF_CHGBODY</c>).
    /// </summary>
    ChangeBodyChunks = 0x02,

    /// <summary>
    /// The filter may quarantine the message (<c>SMFIF_QUARANTINE</c>).
    /// </summary>
    QuarantineMessage = 0x20,

    /// <summary>
    /// Every modification at once.
    /// </summary>
    All = AddHeaders | ChangeBodyChunks | AddRecipients | DeleteRecipients | ChangeOrDeleteHeaders | QuarantineMessage,
}
