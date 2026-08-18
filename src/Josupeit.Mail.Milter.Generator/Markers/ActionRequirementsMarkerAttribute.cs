// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Marks the enum the ActionRequirements property is typed as, which is how the generator tells the two
// static properties on the context interface apart.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Enum)]
[Embedded]
internal sealed partial class ActionRequirementsMarkerAttribute : Attribute
{
}
