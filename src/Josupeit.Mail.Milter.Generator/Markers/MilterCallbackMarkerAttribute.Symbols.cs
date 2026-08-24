// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

internal sealed partial class MilterCallbackMarkerAttribute
{
    private static readonly Type _self = typeof(MilterCallbackMarkerAttribute);

    internal static int GetMinimumProtocolVersion(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, "MinimumProtocolVersion", StringComparison.Ordinal))
                return argument.Value.Value as int? ?? 0;
        }

        return 0;
    }

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        return string.Equals(attribute.ToDisplayString(), _self.FullName, StringComparison.Ordinal);
    }

    internal static string GetProtocolRequirementName(AttributeData attribute)
    {
        return (string)attribute.ConstructorArguments[0].Value!;
    }

    internal static string? GetNoReplyRequirementName(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length < 2)
            return null;

        return attribute.ConstructorArguments[1].Value as string;
    }
}
