// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

internal sealed partial class MilterCallbackMarkerAttribute
{
    private static readonly Type _self = typeof(MilterCallbackMarkerAttribute);

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        return string.Equals(attribute.ToDisplayString(), _self.FullName, StringComparison.Ordinal);
    }

    internal static string GetProtocolRequirementName(AttributeData attribute)
    {
        var value = attribute.ConstructorArguments.Single();

        return (string)value.Value!;
    }
}
