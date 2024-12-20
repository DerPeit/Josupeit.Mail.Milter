using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MilterCallbackMarkerAttribute(string protocolRequirementName) : Attribute
{
    private static readonly Type _self = typeof(MilterCallbackMarkerAttribute);

    public string ProtocolRequirementName { get; } = protocolRequirementName;

    internal static bool IsDescribedBy(INamedTypeSymbol attribute)
    {
        var isCorrectAssemblyName = string.Equals(attribute.ContainingAssembly.ToDisplayString(), _self.Assembly.FullName);
        var isCorrectTypeName = string.Equals(attribute.ToDisplayString(), _self.FullName);

        return isCorrectAssemblyName && isCorrectTypeName;
    }

    internal static string GetProtocolRequirementName(AttributeData attribute)
    {
        var value = attribute.ConstructorArguments.Single();
        
        return (string)value.Value!;
    }
}