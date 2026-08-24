// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// Names the DeliveryRequirements member a filter needs before the marked member can tell it anything.
// Reading that member is the whole evidence: a filter that never looks at whether a recipient was
// rejected has no reason to ask the MTA to send rejected recipients.
//
// This half is compiled into the library as well, which is why it names nothing from Roslyn. See
// MilterDeliveryMarkerAttribute.Symbols.cs for the half that does.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
[Embedded]
internal sealed partial class MilterDeliveryMarkerAttribute(string deliveryRequirementName) : Attribute
{
    public string DeliveryRequirementName { get; } = deliveryRequirementName;
}
