// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Names the ProtocolRequirements member a callback asks the MTA for, and the NoReplyRequirements member
// that says the MTA need not wait for its answer. A callback without the first is a callback with no
// flag to clear - OnDefineMacro and OnEndOfBody are both like that - and one without the second is a
// callback whose answer the MTA always waits for.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn.
[AttributeUsage(AttributeTargets.Method)]
[Embedded]
internal sealed partial class MilterCallbackMarkerAttribute(string protocolRequirementName, string? noReplyRequirementName = null) : Attribute
{
    public string ProtocolRequirementName { get; } = protocolRequirementName;

    public string? NoReplyRequirementName { get; } = noReplyRequirementName;

    // The lowest protocol version an MTA must speak before this event can be delivered. Zero means the
    // event has always existed.
    public int MinimumProtocolVersion { get; set; }
}
