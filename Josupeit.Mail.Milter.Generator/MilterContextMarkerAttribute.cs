using System;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

[AttributeUsage(AttributeTargets.Interface)]
public sealed class MilterContextMarkerAttribute : Attribute
{
    private static readonly Type _self = typeof(MilterContextMarkerAttribute);

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        var isCorrectAssemblyName = string.Equals(attribute.ContainingAssembly.ToDisplayString(), _self.Assembly.FullName);
        var isCorrectTypeName = string.Equals(attribute.ToDisplayString(), _self.FullName);

        return isCorrectAssemblyName && isCorrectTypeName;
    }
}