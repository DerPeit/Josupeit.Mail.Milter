// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter;

public static class CommitActions
{
    public static AcceptAction Accept => AcceptAction.Instance;
    public static ContinueAction Continue => ContinueAction.Instance;
    public static DiscardAction Discard => DiscardAction.Instance;
    public static RejectAction Reject => RejectAction.Instance;
    public static DeferAction Defer => DeferAction.Instance;

    public static ReplyAction Reply(int code, string text)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(code, 400);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(code, 600);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfContainsControlCharacter(text);

        return new ReplyAction(code, text);
    }
}