// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Marks a protocol stage the MTA defines a macro class for. Two stages have none - a header and a body
// chunk carry no macros of their own - so a macro list cannot name either, and the generator has to know
// which without knowing what the stages are called.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Field)]
[Embedded]
internal sealed partial class MilterMacroStageMarkerAttribute : Attribute
{
}
