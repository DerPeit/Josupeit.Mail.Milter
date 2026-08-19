// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

internal sealed partial class MilterMacroSourceMarkerAttribute
{
    private static readonly Type _self = typeof(MilterMacroSourceMarkerAttribute);

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        return string.Equals(attribute.ToDisplayString(), _self.FullName, StringComparison.Ordinal);
    }

    internal static bool DescribesMacros(ISymbol symbol)
    {
        return Describes(symbol, wantStage: false);
    }

    internal static bool DescribesStage(ISymbol symbol)
    {
        return Describes(symbol, wantStage: true);
    }

    private static bool Describes(ISymbol symbol, bool wantStage)
    {
        var marker = symbol
            .OriginalDefinition
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass != null && IsDescribedBy(attribute.AttributeClass));

        if (marker == null)
            return false;

        var isStage = marker.ConstructorArguments.Length > 0 && marker.ConstructorArguments[0].Value as bool? == true;

        return isStage == wantStage;
    }
}
