// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

    // The oldest protocol version the library speaks, and the interface default of
    // ProtocolVersionRequirement: the version fold starts here, and a fold that never rose above it
    // derived nothing a class without the generator would miss.
    private const int PROTOCOL_FLOOR = 2;

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
        var macroProperty = FindRequirementProperty(contextInterface, MacroRequirementsMarkerAttribute.IsDescribedBy);
        var versionProperty = FindRequirementProperty(contextInterface, ProtocolVersionRequirementMarkerAttribute.IsDescribedBy);

        var properties = ImmutableArray.CreateBuilder<string>(initialCapacity: 6);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // What a class that is not partial would have to declare by hand, because the generator cannot
        // write into it. The two abstract properties always; the four virtual ones only where what was
        // derived differs from the interface default, since that default is exactly what such a class
        // gets. NoReplyRequirements never: its default costs a round trip per event and nothing else.
        var declareByHand = new List<string>(capacity: 6);

        // The lowest protocol version this filter can work against, taken as the maximum over everything
        // the other four properties turned out to need. No separate walk: each marker already visited
        // carries the version its feature arrived in.
        //
        // Starts at the protocol floor rather than at zero: a filter that uses nothing newer still needs
        // an MTA that speaks version 2, and zero names no member of the enum.
        var minimumProtocolVersion = PROTOCOL_FLOOR;

        if (NeedsGenerating(contextType, protocolProperty))
        {
            var requirements = DeriveProtocolRequirements(contextType, contextInterface, protocolProperty!.Type, ref minimumProtocolVersion);

            properties.Add(RenderProperty(protocolProperty, requirements));
            declareByHand.Add(protocolProperty.Name);
        }

        if (NeedsGenerating(contextType, actionProperty))
        {
            declareByHand.Add(actionProperty!.Name);

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
            {
                declareByHand.Add(deliveryProperty.Name);

                minimumProtocolVersion = Math.Max(minimumProtocolVersion, VersionOfRequirementsEnum(deliveryProperty.Type, DeliveryRequirementsMarkerAttribute.IsDescribedBy, DeliveryRequirementsMarkerAttribute.GetMinimumProtocolVersion));
            }
        }

        if (NeedsGenerating(contextType, noReplyProperty))
        {
            var noReply = NoReplyRequirementsAnalysis.Run(contextType, contextInterface, context.SemanticModel.Compilation, cancellationToken);

            // No contribution to the version fold, deliberately. An MTA below version 6 has the no-reply
            // bits masked away and waits for every answer, which is what it did before any of this
            // existed - so demanding version 6 for it would refuse an MTA the filter works perfectly
            // well against, in exchange for a round trip per event.
            properties.Add(RenderProperty(noReplyProperty!, Combine(noReplyProperty!.Type, noReply)));
        }

        if (NeedsGenerating(contextType, macroProperty))
        {
            var macros = MacroRequirementsAnalysis.Run(contextType, contextInterface, context.SemanticModel.Compilation, cancellationToken);

            // A read that could not be followed is in the list too: MILT003 asks for a hand-written
            // declaration either way.
            if (!macros.IsComplete || macros.ByStage.Count > 0)
                declareByHand.Add(macroProperty!.Name);

            if (!macros.IsComplete)
                diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.UntrackedMacro, macros.UntrackedLocation, contextType.Name, macros.UntrackedSource));
            else if (RenderMacroRequirements(macroProperty!.Type, macros.ByStage) is { } expression)
                properties.Add(RenderProperty(macroProperty, expression));
            else
                diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.UntrackedMacro, contextType.Locations.FirstOrDefault(), contextType.Name, "a macro read at a stage the protocol gives no macro class"));

            // No contribution to the version fold either, and not because leaving the list out is
            // harmless. Below version 6 the list is not sent and the MTA sends its configured set instead,
            // which holds what MTAs are commonly configured with (Postfix by default: nothing but the
            // queue id "i" from DATA onwards), not necessarily what this filter reads. Raising the floor
            // would turn a missing macro into a failed negotiation, but it would also refuse every older
            // MTA to a filter reading only "i", the commonest read of all - and telling the two apart
            // takes the MTA's configuration, which the generator cannot see. MacroRequirements documents
            // the gap instead.
        }

        // Last, because it is a fold over what the four above turned out to need.
        if (NeedsGenerating(contextType, versionProperty))
        {
            properties.Add(RenderProperty(versionProperty!, CombineByValue(versionProperty!.Type, minimumProtocolVersion)));

            if (minimumProtocolVersion > PROTOCOL_FLOOR)
                declareByHand.Add(versionProperty.Name);
        }

        // Nothing can be written into a class that is not partial. That only deserves an error where the
        // defaults it falls back to would be wrong for it: a context that declared the two abstract
        // properties and uses nothing the other four govern works as it is, and is left alone.
        if (!EnclosingDeclarations(declaration).All(IsPartial))
        {
            if (declareByHand.Count > 0)
            {
                var notPartial = EnclosingDeclarations(declaration)
                    .Where(type => !IsPartial(type))
                    .Reverse()
                    .Select(type => type.Identifier.ValueText)
                    .ToList();

                diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.ContextIsNotPartial, declaration.Identifier.GetLocation(), contextType.Name, JoinNames(notPartial), JoinNames(declareByHand)));
            }

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

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The ban is about milter frames, and this is C# source. SourceText.From needs an encoding with a preamble or the sources cannot be embedded in the symbol package.")]
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

        // ReSharper disable once AccessToModifiedClosure, because it's instantly handled
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

    // Renders the chained factory call the macro requirements are built with. Null when a stage has no
    // macro class in the protocol - Header and Body - because emitting a call that throws at runtime
    // would be worse than refusing to emit at all.
    private static string? RenderMacroRequirements(ITypeSymbol requirementsType, IReadOnlyDictionary<string, IReadOnlyList<string>> byStage)
    {
        var typeName = requirementsType.ToDisplayString();

        if (byStage.Count == 0)
            return $"{typeName}.None";

        // Read off the factory the generated line calls rather than named here: the generator knows
        // MacroRequirements only through its marker attribute, and For's first parameter is the very stage
        // the emitted call has to spell.
        var stageEnum = requirementsType
            .GetMembers("For")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method.Parameters.Length > 0)
            ?.Parameters[0]
            .Type;

        if (stageEnum == null)
            return null;

        // Only the stages the protocol gives a macro class to. A read at one of the others cannot be
        // asked for, and emitting it would produce a call that throws when the filter is first used.
        var declared = stageEnum
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Where(member => member.IsConst && MilterMacroStageMarkerAttribute.Describes(member))
            .ToImmutableArray();
        var rendered = new List<string>(byStage.Count);

        // Emitted in the stage's own declaration order, so the generated line does not churn when the
        // analysis happens to visit the reads in a different order.
        foreach (var member in declared)
        {
            if (!byStage.TryGetValue(member.Name, out var names))
                continue;

            var arguments = string.Join(", ", names.Select(name => SymbolDisplay.FormatLiteral(name, quote: true)));

            rendered.Add($"{stageEnum.ToDisplayString()}.{member.Name}, {arguments}");
        }

        if (rendered.Count != byStage.Count)
            return null;

        var head = $"{typeName}.For({rendered[0]})";

        return rendered.Skip(1).Aggregate(head, (accumulated, arguments) => $"{accumulated}.And({arguments})");
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

    // 'A', 'A' and 'B', 'A', 'B' and 'C' - a diagnostic message reads as a sentence.
    private static string JoinNames(IReadOnlyList<string> names)
    {
        var quoted = names.Select(name => $"'{name}'").ToList();

        return quoted.Count == 1
            ? quoted[0]
            : $"{string.Join(", ", quoted.Take(quoted.Count - 1))} and {quoted[quoted.Count - 1]}";
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
