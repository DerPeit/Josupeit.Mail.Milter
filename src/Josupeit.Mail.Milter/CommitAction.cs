// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

/// <summary>
/// An action that decides what the MTA does with the message.
/// </summary>
/// <remarks>
/// Exactly one of these ends every callback: the event callbacks return one directly, and
/// <see cref="IMilterCallbacks.OnEndOfBody"/> must yield one as the last element of its sequence. Obtain
/// them from <see cref="CommitActions"/>.
/// </remarks>
public abstract record CommitAction : Action
{
    private protected CommitAction()
    {
    }
}
