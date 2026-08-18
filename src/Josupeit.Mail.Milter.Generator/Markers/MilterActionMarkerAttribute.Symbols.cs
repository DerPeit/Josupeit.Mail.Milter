// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

// The half only the generator compiles. Matching is by full type name and nothing else: the library
// carries its own copy of the declaration, so the attribute the generator finds on a symbol lives in a
// different assembly from the one it can call typeof on.
internal sealed partial class MilterActionMarkerAttribute
{
    private static readonly Type _self = typeof(MilterActionMarkerAttribute);

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        return string.Equals(attribute.ToDisplayString(), _self.FullName, StringComparison.Ordinal);
    }

    internal static string? GetActionRequirementName(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return null;

        return attribute.ConstructorArguments[0].Value as string;
    }
}
