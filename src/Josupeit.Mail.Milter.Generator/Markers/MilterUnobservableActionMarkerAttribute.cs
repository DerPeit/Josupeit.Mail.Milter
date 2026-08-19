// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Marks a commit action whose delivery the MTA cannot observe, so a callback that can only ever answer
// with it need not answer at all.
//
// Says why rather than which, deliberately: the generator must not know that the action in question is
// called ContinueAction, only that answering it and not answering are the same thing to the MTA. That
// is exactly the property the no-reply negotiation trades on.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Class)]
[Embedded]
internal sealed partial class MilterUnobservableActionMarkerAttribute : Attribute
{
}
