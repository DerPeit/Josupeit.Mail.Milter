// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Marks the interface a filter implements, which is how the generator finds a context class without
// knowing the name IMilterContext.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Interface)]
[Embedded]
internal sealed partial class MilterContextMarkerAttribute : Attribute
{
}
