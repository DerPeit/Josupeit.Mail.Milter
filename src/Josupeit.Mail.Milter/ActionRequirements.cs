// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The modifications a filter intends to perform, declared to the MTA when the connection is negotiated.
/// </summary>
/// <remarks>
/// This is a promise made up front, not a capability discovered later: the MTA refuses any modification
/// it was not warned about. Declare exactly what the filter will do - claiming more grants the filter
/// rights over the message it does not need, and claiming less makes the modification fail at the moment
/// it matters.
/// </remarks>
[Flags]
public enum ActionRequirements
{
    /// <summary>
    /// The filter inspects messages but never changes them.
    /// </summary>
    None = 0,

    /// <summary>
    /// The filter may add envelope recipients. See <see cref="ModificationActions.AddRecipient"/>.
    /// </summary>
    AddRecipients = 1,

    /// <summary>
    /// The filter may remove envelope recipients. See <see cref="ModificationActions.DeleteRecipient"/>.
    /// </summary>
    DeleteRecipients = 2,

    /// <summary>
    /// The filter may add headers. See <see cref="ModificationActions.AddHeader"/>.
    /// </summary>
    AddHeaders = 4,

    /// <summary>
    /// The filter may change existing headers. See <see cref="ModificationActions.ChangeHeader"/>.
    /// </summary>
    ChangeHeaders = 8,

    /// <summary>
    /// The filter may remove headers. See <see cref="ModificationActions.DeleteHeader"/>.
    /// </summary>
    DeleteHeaders = 16,

    /// <summary>
    /// The filter may replace the message body. See <see cref="ModificationActions.ReplaceBody"/>.
    /// </summary>
    ChangeBody = 32,

    /// <summary>
    /// The filter may quarantine the message. See <see cref="ModificationActions.QuarantineMessage"/>.
    /// </summary>
    QuarantineMessage = 64,

    /// <summary>
    /// Every modification at once.
    /// </summary>
    All = AddRecipients | DeleteRecipients | AddHeaders | ChangeHeaders | DeleteHeaders | ChangeBody | QuarantineMessage,
}