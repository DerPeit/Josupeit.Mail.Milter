// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator.Markers;

internal sealed partial class MilterUnobservableActionMarkerAttribute
{
    private static readonly Type _self = typeof(MilterUnobservableActionMarkerAttribute);

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        return string.Equals(attribute.ToDisplayString(), _self.FullName, StringComparison.Ordinal);
    }

    internal static bool Describes(ITypeSymbol type)
    {
        return type
            .GetAttributes()
            .Any(attribute => attribute.AttributeClass != null && IsDescribedBy(attribute.AttributeClass));
    }
}
