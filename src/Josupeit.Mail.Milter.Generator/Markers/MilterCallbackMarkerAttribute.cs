// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Names the ProtocolRequirements member a callback asks the MTA for. A callback without one is a
// callback with no flag to clear - OnDefineMacro and OnEndOfBody are both like that.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Method)]
[Embedded]
internal sealed partial class MilterCallbackMarkerAttribute(string protocolRequirementName) : Attribute
{
    public string ProtocolRequirementName { get; } = protocolRequirementName;
}
