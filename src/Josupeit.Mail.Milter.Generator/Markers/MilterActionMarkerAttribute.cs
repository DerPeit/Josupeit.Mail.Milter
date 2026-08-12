// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Names the ActionRequirements member a concrete action needs, or - applied without one - marks an
// abstract type a modification can hide behind. The generator needs both halves: the first is how a
// yielded action turns into a flag, the second is how it recognises that it has lost track of one.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn. See
// MilterActionMarkerAttribute.Symbols.cs for the half that does.
[AttributeUsage(AttributeTargets.Class)]
[Embedded]
internal sealed partial class MilterActionMarkerAttribute(string? actionRequirementName = null) : Attribute
{
    public string? ActionRequirementName { get; } = actionRequirementName;
}
