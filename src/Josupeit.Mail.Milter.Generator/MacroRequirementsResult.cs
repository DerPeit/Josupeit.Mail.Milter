// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

// What the macro analysis found. Either it pinned every read down to a constant name under a known
// stage, or it lost one - and then the answer is a diagnostic, because a list that leaves a macro out
// fails nowhere near where it was caused: the filter simply finds the key missing, on some message, in
// production.
internal readonly record struct MacroRequirementsResult(
    ImmutableDictionary<string, IReadOnlyList<string>> ByStage,
    string? UntrackedSource,
    Location? UntrackedLocation)
{
    public static readonly MacroRequirementsResult Empty = new(ImmutableDictionary<string, IReadOnlyList<string>>.Empty, null, null);

    public bool IsComplete => UntrackedSource == null;

    public static MacroRequirementsResult Found(ImmutableDictionary<string, IReadOnlyList<string>> byStage)
    {
        return new MacroRequirementsResult(byStage, null, null);
    }

    public static MacroRequirementsResult Lost(string source, Location? location)
    {
        return new MacroRequirementsResult(ImmutableDictionary<string, IReadOnlyList<string>>.Empty, source, location);
    }
}
