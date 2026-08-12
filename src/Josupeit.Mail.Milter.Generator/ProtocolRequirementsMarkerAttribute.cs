// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

[AttributeUsage(AttributeTargets.Enum)]
public sealed class ProtocolRequirementsMarkerAttribute : Attribute
{
    private static readonly Type _self = typeof(ProtocolRequirementsMarkerAttribute);

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        var isCorrectAssemblyName = string.Equals(attribute.ContainingAssembly.ToDisplayString(), _self.Assembly.FullName);
        var isCorrectTypeName = string.Equals(attribute.ToDisplayString(), _self.FullName);

        return isCorrectAssemblyName && isCorrectTypeName;
    }
}