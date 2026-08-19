// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Josupeit.Mail.Milter.Generator;

// Writes the requirement properties of IMilterContext for a filter that did not write them itself.
//
// ProtocolRequirements comes from which callbacks the class overrode: an implementation that is still
// the interface default is an event the filter does not want delivered. ActionRequirements comes from
// which actions its code builds - see ActionRequirementsAnalysis for how far that can be taken.
// DeliveryRequirements comes from whether the code reads a member that only means something once the
// matching flag has been negotiated.
//
// A property the class declares by hand is left alone, so the generator never argues with an author who
// has decided otherwise.
[Generator(LanguageNames.CSharp)]
public sealed class MilterContextSourceGenerator : IIncrementalGenerator
{
    private const string GENERATED_MEMBER_ATTRIBUTES =
        """
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
        [System.Diagnostics.DebuggerNonUserCodeAttribute]
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        """;

    // Environment.NewLine is off limits inside a compiler component - a generator that emitted a
    // different file on Linux than on Windows would break deterministic builds.
    private const string NEW_LINE = "\r\n";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var emissions = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (syntaxContext, cancellationToken) => Analyse(syntaxContext, cancellationToken))
            .Where(static emission => emission != null);

        context.RegisterSourceOutput(emissions, static (sourceContext, emission) => Emit(sourceContext, emission!));
    }

    private static MilterContextEmission? Analyse(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not { } contextType)
            return null;

        var contextInterface = contextType.AllInterfaces.FirstOrDefault(HasContextMarker);
        if (contextInterface == null)
            return null;

        // A partial class arrives once per part. Only the first of them writes, or the compiler gets the
        // same member twice.
        if (!IsPrimaryDeclaration(contextType, declaration))
            return null;

        var protocolProperty = FindRequirementProperty(contextInterface, ProtocolRequirementsMarkerAttribute.IsDescribedBy);
        var actionProperty = FindRequirementProperty(contextInterface, ActionRequirementsMarkerAttribute.IsDescribedBy);
        var deliveryProperty = FindRequirementProperty(contextInterface, DeliveryRequirementsMarkerAttribute.IsDescribedBy);
        var noReplyProperty = FindRequirementProperty(contextInterface, NoReplyRequirementsMarkerAttribute.IsDescribedBy);
        var versionProperty = FindRequirementProperty(contextInterface, ProtocolVersionRequirementMarkerAttribute.IsDescribedBy);

        var properties = ImmutableArray.CreateBuilder<string>(initialCapacity: 5);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // The lowest protocol version this filter can work against, taken as the maximum over everything
        // the other four properties turned out to need. No separate walk: each marker already visited
        // carries the version its feature arrived in.
        //
        // Starts at the protocol floor rather than at zero: a filter that uses nothing newer still needs
        // an MTA that speaks version 2, and zero names no member of the enum.
        var minimumProtocolVersion = 2;

        if (NeedsGenerating(contextType, protocolProperty))
        {
            var requirements = DeriveProtocolRequirements(contextType, contextInterface, protocolProperty!.Type, ref minimumProtocolVersion);

            properties.Add(RenderProperty(protocolProperty, requirements));
        }

        if (NeedsGenerating(contextType, actionProperty))
        {
            var analysis = ActionRequirementsAnalysis.Run(contextType, context.SemanticModel.Compilation, cancellationToken);

            if (analysis.IsComplete)
            {
                properties.Add(RenderProperty(actionProperty!, Combine(actionProperty!.Type, analysis.RequirementNames)));

                if (analysis.MinimumProtocolVersion > minimumProtocolVersion)
                    minimumProtocolVersion = analysis.MinimumProtocolVersion;
            }
            else
                diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.UntrackedAction, analysis.UntrackedLocation, contextType.Name, analysis.UntrackedSource));
        }


        if (NeedsGenerating(contextType, deliveryProperty))
        {
            var delivery = DeliveryRequirementsAnalysis.Run(contextType, context.SemanticModel.Compilation, cancellationToken);

            properties.Add(RenderProperty(deliveryProperty!, Combine(deliveryProperty!.Type, delivery)));

            if (delivery.Length > 0)
                minimumProtocolVersion = Math.Max(minimumProtocolVersion, VersionOfRequirementsEnum(deliveryProperty!.Type, DeliveryRequirementsMarkerAttribute.IsDescribedBy, DeliveryRequirementsMarkerAttribute.GetMinimumProtocolVersion));
        }

        if (NeedsGenerating(contextType, noReplyProperty))
        {
            var noReply = NoReplyRequirementsAnalysis.Run(contextType, contextInterface, context.SemanticModel.Compilation, cancellationToken);

            properties.Add(RenderProperty(noReplyProperty!, Combine(noReplyProperty!.Type, noReply)));

            if (noReply.Length > 0)
                minimumProtocolVersion = Math.Max(minimumProtocolVersion, VersionOfRequirementsEnum(noReplyProperty!.Type, NoReplyRequirementsMarkerAttribute.IsDescribedBy, NoReplyRequirementsMarkerAttribute.GetMinimumProtocolVersion));
        }

        // Last, because it is a fold over what the four above turned out to need.
        if (NeedsGenerating(contextType, versionProperty))
            properties.Add(RenderProperty(versionProperty!, CombineByValue(versionProperty!.Type, minimumProtocolVersion)));

        // Only worth complaining about once there is something to write. A context that declared both
        // properties by hand has no reason to be partial.
        if (properties.Count > 0 && !EnclosingDeclarations(declaration).All(IsPartial))
        {
            diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.ContextIsNotPartial, declaration.Identifier.GetLocation(), contextType.Name));

            properties.Clear();
        }

        return CreateEmission(contextType, declaration, properties.ToImmutable(), diagnostics.ToImmutable());
    }

    private static MilterContextEmission CreateEmission(
        INamedTypeSymbol contextType,
        ClassDeclarationSyntax declaration,
        ImmutableArray<string> properties,
        ImmutableArray<Diagnostic> diagnostics)
    {
        var containing = EnclosingDeclarations(declaration)
            .Skip(1)
            .Reverse()
            .Select(RenderTypeDeclaration)
            .ToImmutableArray();

        var @namespace = contextType.ContainingNamespace.IsGlobalNamespace
            ? null
            : contextType.ContainingNamespace.ToDisplayString();

        return new MilterContextEmission(
            HintName: CreateHintName(contextType),
            Namespace: @namespace,
            ContainingTypes: containing,
            TypeDeclaration: RenderTypeDeclaration(declaration),
            Properties: properties,
            Diagnostics: diagnostics);
    }

    private static void Emit(SourceProductionContext context, MilterContextEmission emission)
    {
        foreach (var diagnostic in emission.Diagnostics)
            context.ReportDiagnostic(diagnostic);

        if (emission.Properties.Length == 0)
            return;

        var source = new StringBuilder("// <auto-generated/>").AppendLine().AppendLine("#nullable enable").AppendLine();
        var depth = 0;

        if (emission.Namespace != null)
        {
            source.Append("namespace ").Append(emission.Namespace).AppendLine();
            source.AppendLine("{");

            depth++;
        }

        foreach (var containing in emission.ContainingTypes)
        {
            source.Append(Indent(depth)).Append(containing).AppendLine();
            source.Append(Indent(depth)).AppendLine("{");

            depth++;
        }

        source.Append(Indent(depth)).Append(emission.TypeDeclaration).AppendLine();
        source.Append(Indent(depth)).AppendLine("{");

        source.AppendLine(string.Join(NEW_LINE + NEW_LINE, emission.Properties.Select(property => Reindent(property, depth + 1))));

        source.Append(Indent(depth)).AppendLine("}");

        for (depth--; depth >= 0; depth--)
            source.Append(Indent(depth)).AppendLine("}");

        context.AddSource(emission.HintName, SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static string DeriveProtocolRequirements(
        INamedTypeSymbol contextType,
        INamedTypeSymbol contextInterface,
        ITypeSymbol requirementsEnum,
        ref int minimumProtocolVersion)
    {
        var overridden = new HashSet<string>(StringComparer.Ordinal);

        var callbacks = contextInterface.AllInterfaces
            .Append(contextInterface)
            .SelectMany(@interface => @interface.GetMembers())
            .OfType<IMethodSymbol>();

        foreach (var callback in callbacks)
        {
            var marker = callback
                .GetAttributes()
                .FirstOrDefault(attribute => attribute.AttributeClass != null && MilterCallbackMarkerAttribute.IsDescribedBy(attribute.AttributeClass));

            if (marker == null)
                continue;

            var implementation = contextType.FindImplementationForInterfaceMember(callback);

            // The interface hands back its own default when nothing overrode it, and an event nobody
            // wrote a body for is an event there is no point asking the MTA to send.
            if (implementation == null || implementation.Equals(callback, SymbolEqualityComparer.Default))
                continue;

            overridden.Add(MilterCallbackMarkerAttribute.GetProtocolRequirementName(marker));

            var version = MilterCallbackMarkerAttribute.GetMinimumProtocolVersion(marker);
            if (version > minimumProtocolVersion)
                minimumProtocolVersion = version;
        }

        return Combine(requirementsEnum, overridden);
    }

    // Spells the flags out in the order the enum declares them rather than the order they were found, so
    // the generated line reads like the enum does and does not churn when the analysis changes.
    private static string Combine(ITypeSymbol requirementsEnum, ICollection<string> names)
    {
        var typeName = requirementsEnum.ToDisplayString();

        var declared = requirementsEnum
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Where(member => member.IsConst && names.Contains(member.Name))
            .Select(member => $"{typeName}.{member.Name}")
            .ToImmutableArray();

        if (declared.Length == 0)
            return $"default({typeName})";

        return string.Join(" | ", declared);
    }


    // Reads the version an enum's own marker declares, for the two whose every member needs the same one.
    private static int VersionOfRequirementsEnum(ITypeSymbol requirementsEnum, Func<INamedTypeSymbol, bool> isMarker, Func<AttributeData, int> getVersion)
    {
        var marker = requirementsEnum
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass != null && isMarker(attribute.AttributeClass));

        return marker == null ? 0 : getVersion(marker);
    }

    // The one property resolved by value rather than by name. The others match a member the markers
    // named; this one has computed a number and has to find the member that carries it - and must not
    // invent one, because a cast would print a version the enum does not declare.
    private static string CombineByValue(ITypeSymbol requirementsEnum, int value)
    {
        var typeName = requirementsEnum.ToDisplayString();

        var declared = requirementsEnum
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Where(member => member.IsConst && member.ConstantValue is int declaredValue && declaredValue == value)
            .Select(member => $"{typeName}.{member.Name}")
            .FirstOrDefault();

        return declared ?? $"default({typeName})";
    }
    private static string RenderProperty(IPropertySymbol property, string expression)
    {
        return
            $"""
             {GENERATED_MEMBER_ATTRIBUTES}
             public static {property.Type.ToDisplayString()} {property.Name} => {expression};
             """;
    }

    private static bool NeedsGenerating(INamedTypeSymbol contextType, IPropertySymbol? property)
    {
        if (property == null)
            return false;

        var implementation = contextType.FindImplementationForInterfaceMember(property);

        // An abstract property has no implementation until the class writes one. A virtual property
        // always has one - the interface hands back its own default - so "nobody wrote this" is the same
        // test the callbacks already use.
        return implementation == null || implementation.Equals(property, SymbolEqualityComparer.Default);
    }

    private static IPropertySymbol? FindRequirementProperty(INamedTypeSymbol contextInterface, Func<INamedTypeSymbol, bool> isMarker)
    {
        return contextInterface
            .GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(property => property.IsStatic && property.Type.GetAttributes().Any(attribute => attribute.AttributeClass != null && isMarker(attribute.AttributeClass)));
    }

    private static bool HasContextMarker(INamedTypeSymbol candidate)
    {
        return candidate
            .GetAttributes()
            .Any(attribute => attribute.AttributeClass != null && MilterContextMarkerAttribute.IsDescribedBy(attribute.AttributeClass));
    }

    private static bool IsPrimaryDeclaration(INamedTypeSymbol contextType, ClassDeclarationSyntax declaration)
    {
        var primary = contextType.DeclaringSyntaxReferences
            .OrderBy(reference => reference.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(reference => reference.Span.Start)
            .FirstOrDefault();

        return primary != null
            && primary.SyntaxTree == declaration.SyntaxTree
            && primary.Span == declaration.Span;
    }

    private static IEnumerable<TypeDeclarationSyntax> EnclosingDeclarations(ClassDeclarationSyntax declaration)
    {
        for (SyntaxNode? node = declaration; node is TypeDeclarationSyntax type; node = node.Parent)
            yield return type;
    }

    private static bool IsPartial(TypeDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
    }

    private static string RenderTypeDeclaration(TypeDeclarationSyntax declaration)
    {
        var keyword = declaration is RecordDeclarationSyntax record
            ? $"record {record.ClassOrStructKeyword.ValueText}".TrimEnd()
            : declaration.Keyword.ValueText;

        return $"partial {keyword} {declaration.Identifier.ValueText}{declaration.TypeParameterList?.ToString()}";
    }

    private static string CreateHintName(INamedTypeSymbol contextType)
    {
        var name = contextType
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted))
            .Replace('<', '{')
            .Replace('>', '}');

        return $"{name}.g.cs";
    }

    private static string Indent(int depth)
    {
        return new string(' ', depth * 4);
    }

    private static string Reindent(string block, int depth)
    {
        var indent = Indent(depth);

        return string.Join(NEW_LINE, block.Split('\n').Select(line => indent + line.TrimEnd('\r')));
    }
}
