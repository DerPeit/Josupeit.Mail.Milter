using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Josupeit.Mail.Milter.Generator;

[Generator]
public class MilterContextSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        //context.RegisterForSyntaxNotifications(() => new DummyReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var typeLocator = new TypeLocator();
        var methodLocator = new MethodLocator();
        var propertyLocator = new PropertyLocator();

        var milterContextInterface = context.Compilation.ExternalReferences
            .Select(context.Compilation.GetAssemblyOrModuleSymbol)
            .Where(x => x != null)
            .SelectMany(x => typeLocator.LocateInterfacesWith<MilterContextMarkerAttribute>(x!))
            .First();

        var protocolRequirementsEnum = typeLocator
            .LocateEnumsWith<ProtocolRequirementsMarkerAttribute>(milterContextInterface.ContainingAssembly)
            .First();

        var protocolRequirementsProperty = propertyLocator
            .LocatePropertiesOfType(milterContextInterface, protocolRequirementsEnum)
            .First();

        var milterCallbacks = methodLocator
            .LocateMethodsWith<MilterCallbackMarkerAttribute>(milterContextInterface)
            .Select(method => new { Method = method, ProtocolRequirementsName = method.GetAttributes<MilterCallbackMarkerAttribute>().Select(x => x.ProtocolRequirementName).Single() })
            .ToList();
        
        var milterContextImplementations = typeLocator
            .LocateClassesImplementingInterface(context.Compilation.Assembly, milterContextInterface);

        foreach (var milterContextImplementation in milterContextImplementations)
        {
            var doesMilterContextDeclareProtocolRequirements = milterContextImplementation.FindImplementationForInterfaceMember(protocolRequirementsProperty) != null;
            if (doesMilterContextDeclareProtocolRequirements)
                continue; // because there is nothing to do for us.

            var protocolRequirementsFullName = protocolRequirementsEnum.ToDisplayString();

            var protocolRequirementsBuilder = new StringBuilder()
                .Append("default(")
                .Append(protocolRequirementsFullName)
                .Append(")");

            foreach (var callbackDeclaration in milterCallbacks)
            {
                var candidate = milterContextImplementation.FindImplementationForInterfaceMember(callbackDeclaration.Method)!;
                
                var isInterfaceDefaultImplementation = candidate.Equals(callbackDeclaration.Method, SymbolEqualityComparer.Default);
                if (isInterfaceDefaultImplementation)
                    continue;

                protocolRequirementsBuilder.Append(" | ");
                protocolRequirementsBuilder.Append(protocolRequirementsFullName);
                protocolRequirementsBuilder.Append('.');
                protocolRequirementsBuilder.Append(callbackDeclaration.ProtocolRequirementsName);
            }

            var sourceCode =
                $$"""
                  namespace {{milterContextImplementation.ContainingNamespace}}
                  {
                      partial class {{milterContextImplementation.Name}}
                      {
                          [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
                          [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
                          [System.Diagnostics.DebuggerHiddenAttribute]
                          [System.Diagnostics.DebuggerNonUserCodeAttribute]
                          [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
                          public static {{protocolRequirementsEnum.ToDisplayString()}} {{protocolRequirementsProperty!.Name}}
                          {
                              get
                              {
                                  return {{protocolRequirementsBuilder}};
                              }
                          }
                      }
                  }
                  """;

            context.AddSource(hintName: milterContextImplementation.Name + ".g.cs", sourceCode);
        }
    }

    //public void Execute(GeneratorExecutionContext context)
    //{
    //    var receiver = (DummyReceiver)context.SyntaxContextReceiver!;

    //    var milterContextImplementations = receiver.MilterContextImplementations;
    //    if (milterContextImplementations == null)
    //        return;

    //    var milterCallbacks = receiver.MilterCallbacks!;
    //    var protocolRequirementsProperty = receiver.ProtocolRequirementsProperty!;
    //    var protocolRequirementsEnum = receiver.ProtocolRequirementsEnum!;

    //    foreach (var milterContextClass in milterContextImplementations)
    //    {
    //        var milterContextClassSymbol = (INamedTypeSymbol)milterContextClass.SemanticModel.GetDeclaredSymbol(milterContextClass.Node)!;
    //        var enumFullName = protocolRequirementsEnum.ToDisplayString();

    //        var protocolRequirements = new StringBuilder()
    //            .Append("default(")
    //            .Append(enumFullName)
    //            .Append(")");

    //        foreach (var (callbackDeclaration, protocolRequirementsEnumName) in milterCallbacks)
    //        {
    //            var callbackImplementationCandidate = milterContextClassSymbol.FindImplementationForInterfaceMember(callbackDeclaration)!;

    //            if (callbackImplementationCandidate.Equals(callbackDeclaration, SymbolEqualityComparer.Default))
    //                continue; // because the interface default body has not been "overridden"

    //            protocolRequirements.Append(" | ");
    //            protocolRequirements.Append(enumFullName);
    //            protocolRequirements.Append('.');
    //            protocolRequirements.Append(protocolRequirementsEnumName);
    //        }

    //        var sourceCode =
    //            $$"""
    //              namespace {{milterContextClassSymbol.ContainingNamespace}}
    //              {
    //                  partial class {{milterContextClassSymbol.Name}}
    //                  {
    //                      [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    //                      [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
    //                      [System.Diagnostics.DebuggerHiddenAttribute]
    //                      [System.Diagnostics.DebuggerNonUserCodeAttribute]
    //                      [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
    //                      public static {{protocolRequirementsEnum.ToDisplayString()}} {{protocolRequirementsProperty.Name}}
    //                      {
    //                          get
    //                          {
    //                              return {{protocolRequirements}};
    //                          }
    //                      }
    //                  }
    //              }
    //              """;

    //        context.AddSource(hintName: milterContextClassSymbol.Name + ".g.cs", sourceCode);
    //    }
    //}
}

file sealed class DummyReceiver : ISyntaxContextReceiver
{
    public INamedTypeSymbol? MilterContextInterface { get; private set; }

    public IList<(IMethodSymbol Callback, string ProtocolRequirementName)>? MilterCallbacks { get; private set; }

    public IList<GeneratorSyntaxContext>? MilterContextImplementations { get; private set; }

    public IPropertySymbol? ProtocolRequirementsProperty { get; private set; }

    public INamedTypeSymbol? ProtocolRequirementsEnum { get; private set; }

    public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classNode)
            return;

        var isPartial = classNode.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword));
        if (!isPartial)
            return;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classNode)!;

        if (MilterContextInterface == null)
        {
            foreach (var iface in classSymbol.AllInterfaces)
            {
                var attrs = iface.GetAttributes().Select(a => a.AttributeClass);
                var hasMarkerAttribute = attrs.Any(a => a != null && MilterContextMarkerAttribute.IsDescribedBy(a));

                if (!hasMarkerAttribute)
                    continue;

                MilterContextInterface = iface;

                break;
            }

            if (MilterContextInterface == null)
                return; // because the class cannot be a context implementation

            foreach (var member in MilterContextInterface.GetMembers())
            {
                if (member is not { Kind: SymbolKind.Property, IsStatic: true })
                    continue;

                var property = (IPropertySymbol)member;
                var protocolRequirementsEnumCandidate = property.Type;
                var candidateAttrs = protocolRequirementsEnumCandidate.GetAttributes().Select(a => a.AttributeClass);

                var hasMarkerAttribute = candidateAttrs.Any(ac => ac != null && ProtocolRequirementsMarkerAttribute.IsDescribedBy(ac));
                if (!hasMarkerAttribute)
                    continue;

                ProtocolRequirementsProperty = property;
                ProtocolRequirementsEnum = (INamedTypeSymbol)protocolRequirementsEnumCandidate;

                break;
            }

            var milterCallbackCandidates = MilterContextInterface.AllInterfaces.SelectMany(iface => iface.GetMembers().OfType<IMethodSymbol>());

            foreach (var milterCallbackCandidate in milterCallbackCandidates)
            {
                var markerAttributeData = milterCallbackCandidate
                    .GetAttributes()
                    .SingleOrDefault(ad => ad.AttributeClass != null && MilterCallbackMarkerAttribute.IsDescribedBy(ad.AttributeClass));

                if (markerAttributeData == null)
                    continue;

                var reqName = MilterCallbackMarkerAttribute.GetProtocolRequirementName(markerAttributeData);

                MilterCallbacks ??= new List<(IMethodSymbol Callback, string ProtocolRequirementName)>(capacity: 8);
                MilterCallbacks.Add((milterCallbackCandidate, reqName));
            }

            MilterContextImplementations ??= new List<GeneratorSyntaxContext>();
            MilterContextImplementations.Add(context);

            return;
        }

        var isMilterContextImpl = classSymbol.AllInterfaces.Any(iface => iface.Equals(MilterContextInterface, SymbolEqualityComparer.Default));

        if (!isMilterContextImpl)
            return;

        MilterContextImplementations ??= new List<GeneratorSyntaxContext>();
        MilterContextImplementations.Add(context);
    }
}