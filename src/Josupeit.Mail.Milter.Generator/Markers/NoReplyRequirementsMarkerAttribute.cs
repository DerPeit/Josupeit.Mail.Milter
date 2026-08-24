// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Marks the enum the NoReplyRequirements property is typed as, which is how the generator picks that
// property out of the several static ones the context interface declares.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Enum)]
[Embedded]
internal sealed partial class NoReplyRequirementsMarkerAttribute : Attribute
{
    // Every member of this enum needs the same protocol version, so it is named once here rather than
    // on each of them.
    public int MinimumProtocolVersion { get; set; }
}
