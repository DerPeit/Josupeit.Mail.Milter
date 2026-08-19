// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Marks the two members the macro analysis reads a filter's intent from: the dictionary of macro values,
// and the stage that says which point in the conversation they belong to. Naming them here rather than
// in the generator keeps it ignorant of what the information struct is called.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
[Embedded]
internal sealed partial class MilterMacroSourceMarkerAttribute(bool isStage = false) : Attribute
{
    // False for the macro dictionary, true for the stage it belongs to.
    public bool IsStage { get; } = isStage;
}
