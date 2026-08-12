// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

// Names the ActionRequirements member a concrete action needs, or - applied without one - marks an
// abstract type a modification can hide behind. The generator needs both halves: the first is how a
// yielded action turns into a flag, the second is how it recognises that it has lost track of one.
[AttributeUsage(AttributeTargets.Class)]
public sealed class MilterActionMarkerAttribute(string? actionRequirementName = null) : Attribute
{
    private static readonly Type _self = typeof(MilterActionMarkerAttribute);

    public string? ActionRequirementName { get; } = actionRequirementName;

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        var isCorrectAssemblyName = string.Equals(attribute.ContainingAssembly.ToDisplayString(), _self.Assembly.FullName);
        var isCorrectTypeName = string.Equals(attribute.ToDisplayString(), _self.FullName);

        return isCorrectAssemblyName && isCorrectTypeName;
    }

    internal static string? GetActionRequirementName(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return null;

        return attribute.ConstructorArguments[0].Value as string;
    }
}
