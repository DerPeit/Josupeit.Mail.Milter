// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Builds the actions that decide a message's fate. This is the only way to obtain one.
/// </summary>
/// <remarks>
/// The simple decisions are cached singletons, so returning one costs nothing. Anything carrying a value
/// is validated here, which is why the action records themselves have internal constructors and get-only
/// properties: there is no route to an unvalidated action, not even through a
/// <see langword="with"/> expression.
/// </remarks>
public static class CommitActions
{
    /// <summary>
    /// Accepts the message and stops consulting the filter about it.
    /// </summary>
    public static AcceptAction Accept => AcceptAction.Instance;

    /// <summary>
    /// Decides nothing and lets the MTA carry on to the next event.
    /// </summary>
    public static ContinueAction Continue => ContinueAction.Instance;

    /// <summary>
    /// Stops the MTA sending any more of the event just answered.
    /// </summary>
    /// <remarks>
    /// Only for an event that repeats - a body chunk, a header, a recipient - and only useful once the
    /// filter has seen enough of them. Decides nothing: the message carries on and
    /// <see cref="IMilterCallbacks.OnEndOfBody"/> still runs. Quietly becomes <see cref="Continue"/>
    /// when the MTA did not grant it, and throws when answered from an event that happens once.
    /// </remarks>
    public static SkipRemainingEventsAction SkipRemainingEvents => SkipRemainingEventsAction.Instance;

    /// <summary>
    /// Accepts the message on the wire and then silently drops it.
    /// </summary>
    public static DiscardAction Discard => DiscardAction.Instance;

    /// <summary>
    /// Rejects the message permanently, reported to the sender as a 5xx error.
    /// </summary>
    public static RejectAction Reject => RejectAction.Instance;

    /// <summary>
    /// Rejects the message temporarily, reported to the sender as a 4xx error, so it is retried.
    /// </summary>
    public static DeferAction Defer => DeferAction.Instance;

    /// <summary>
    /// Rejects the message with an SMTP reply of the filter's own wording.
    /// </summary>
    /// <param name="code">
    /// The SMTP status code to answer with. A 4xx code defers and a 5xx code rejects.
    /// </param>
    /// <param name="text">The text that accompanies <paramref name="code"/> in the reply.</param>
    /// <returns>The action to return from a callback.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> is below 400 or 600 or above. A reply that is not a rejection has no
    /// meaning here.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> is empty, white space, or contains a control character. A control
    /// character would end the reply line early and let the rest of the text pose as a reply of its own.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static ReplyAction Reply(int code, string text)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(code, 400);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(code, 600);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfContainsControlCharacter(text);

        return new ReplyAction(code, text);
    }
}
