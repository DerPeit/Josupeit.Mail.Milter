// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

/// <summary>
/// An action that changes the message rather than deciding its fate.
/// </summary>
/// <remarks>
/// Only <see cref="IMilterCallbacks.OnEndOfBody"/> may yield these, and only after the MTA was told to
/// expect them: a modification the filter did not declare in
/// <see cref="IMilterContext.ActionRequirements"/> is refused. Obtain them from
/// <see cref="ModificationActions"/>.
/// </remarks>
public abstract record ModificationAction : Action
{
    private protected ModificationAction()
    {
    }
}