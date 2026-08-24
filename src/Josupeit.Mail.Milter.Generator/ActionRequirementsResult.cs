// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

// What the action analysis found. Either it followed every action the context can build, and
// RequirementNames is the answer, or it lost one - and then the answer is a diagnostic instead, because
// a guess would be wrong in a way that only shows up as the MTA refusing the modification.
internal readonly record struct ActionRequirementsResult(
    ImmutableArray<string> RequirementNames,
    int MinimumProtocolVersion,
    string? UntrackedSource,
    Location? UntrackedLocation)
{
    public bool IsComplete => UntrackedSource == null;
}
