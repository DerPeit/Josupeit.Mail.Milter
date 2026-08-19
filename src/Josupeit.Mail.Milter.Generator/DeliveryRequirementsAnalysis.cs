// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Josupeit.Mail.Milter.Generator;

// Works out which delivery changes a context needs by reading which marked members its code looks at.
//
// A may-analysis like ActionRequirementsAnalysis, and for the same reason: the union of what the code
// mentions is computable where the exact runtime set is not. What differs is the direction of the
// uncertainty, and it is the easier direction. Over-declaring here costs a filter some events it
// ignores; failing to notice a read leaves a flag clear, which is what a version 2 MTA would have done
// anyway. So there is nothing to report and no diagnostic - a member this analysis cannot reach simply
// does not contribute, and a filter that needs the flag regardless declares the property by hand.
//
// That is why this walk has no equivalent of ActionRequirementsAnalysis.Lose.
internal sealed class DeliveryRequirementsAnalysis
{
    private readonly Compilation _compilation;
    private readonly IAssemblySymbol _assembly;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModels = [];
    private readonly HashSet<string> _requirementNames = new(StringComparer.Ordinal);
    private readonly HashSet<ISymbol> _visitedSymbols = new(SymbolEqualityComparer.Default);
    private readonly HashSet<SyntaxNode> _enqueuedNodes = [];
    private readonly Queue<SyntaxNode> _pending = new();

    private DeliveryRequirementsAnalysis(IAssemblySymbol assembly, Compilation compilation, CancellationToken cancellationToken)
    {
        _assembly = assembly;
        _compilation = compilation;
        _cancellationToken = cancellationToken;
    }

    public static ImmutableArray<string> Run(INamedTypeSymbol contextType, Compilation compilation, CancellationToken cancellationToken)
    {
        var analysis = new DeliveryRequirementsAnalysis(contextType.ContainingAssembly, compilation, cancellationToken);

        return analysis.Run(contextType);
    }

    private ImmutableArray<string> Run(INamedTypeSymbol contextType)
    {
        Seed(contextType);

        while (_pending.Count > 0)
            Scan(_pending.Dequeue());

        return _requirementNames.OrderBy(name => name, StringComparer.Ordinal).ToImmutableArray();
    }

    private void Seed(INamedTypeSymbol contextType)
    {
        for (var type = contextType; type != null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            if (type.DeclaringSyntaxReferences.Length > 0)
                EnqueueDeclarations(type);
        }
    }

    private void Scan(SyntaxNode node)
    {
        var model = GetSemanticModel(node.SyntaxTree);

        foreach (var expression in node.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var symbol = model.GetSymbolInfo(expression, _cancellationToken).Symbol;
            if (symbol == null)
                continue;

            var name = RequirementNameOf(symbol);
            if (name != null)
                _requirementNames.Add(name);

            Follow(symbol);
        }
    }

    // Follows a call into its body, so a filter that reads the marked member from a helper is counted
    // the same as one that reads it inline. Bounded to this assembly: a member from anywhere else has no
    // body to read here, and walking the framework would cost far more than the flag is worth.
    private void Follow(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method)
            return;

        var definition = method.OriginalDefinition;

        if (definition.IsAbstract || definition.DeclaringSyntaxReferences.Length == 0)
            return;

        if (!SymbolEqualityComparer.Default.Equals(definition.ContainingAssembly, _assembly))
            return;

        if (!_visitedSymbols.Add(definition))
            return;

        EnqueueDeclarations(definition);
    }

    private static string? RequirementNameOf(ISymbol symbol)
    {
        if (symbol is not IPropertySymbol and not IFieldSymbol)
            return null;

        var marker = symbol
            .OriginalDefinition
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass != null && MilterDeliveryMarkerAttribute.IsDescribedBy(attribute.AttributeClass));

        return marker == null ? null : MilterDeliveryMarkerAttribute.GetDeliveryRequirementName(marker);
    }

    private void EnqueueDeclarations(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax(_cancellationToken);

            if (_enqueuedNodes.Add(node))
                _pending.Enqueue(node);
        }
    }

    private SemanticModel GetSemanticModel(SyntaxTree tree)
    {
        if (_semanticModels.TryGetValue(tree, out var model))
            return model;

        model = _compilation.GetSemanticModel(tree);
        _semanticModels[tree] = model;

        return model;
    }
}
