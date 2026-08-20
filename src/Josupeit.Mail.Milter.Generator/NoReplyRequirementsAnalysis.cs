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

// Works out which events the MTA need not wait for an answer to, by proving that a callback can only
// ever answer one particular way.
//
// This is a must-analysis, and it is the exact inverse of ActionRequirementsAnalysis. That one has to
// over-approximate, because an action it fails to notice is a modification the MTA silently refuses.
// This one has to under-approximate: a callback it cannot prove keeps its reply, which is what the
// library did before any of this existed. So every way of losing track - a helper in another assembly,
// an interface call, a delegate, a body this compilation does not have - lands on the safe side by
// construction, and there is nothing to report and no diagnostic.
//
// Setting the flag is free in the only sense that matters. If a callback can answer nothing but the
// unobservable action, then answering and not answering are the same thing to the MTA, and the filter
// is spared a round trip per event without a single decision changing.
//
// Two conditions, and the second is the one that is easy to forget: every commit action the body
// mentions must be unobservable, AND it must mention at least one. A body that mentions none has not
// been proved safe, it has merely been read - it might be handing back a field, or a task built
// somewhere this analysis cannot see.
internal static class NoReplyRequirementsAnalysis
{
    public static ImmutableArray<string> Run(
        INamedTypeSymbol contextType,
        INamedTypeSymbol contextInterface,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var semanticModels = new Dictionary<SyntaxTree, SemanticModel>();

        var callbacks = contextInterface.AllInterfaces
            .Append(contextInterface)
            .SelectMany(@interface => @interface.GetMembers())
            .OfType<IMethodSymbol>();

        foreach (var callback in callbacks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var marker = callback
                .GetAttributes()
                .FirstOrDefault(attribute => attribute.AttributeClass != null && MilterCallbackMarkerAttribute.IsDescribedBy(attribute.AttributeClass));

            if (marker == null)
                continue;

            var noReplyName = MilterCallbackMarkerAttribute.GetNoReplyRequirementName(marker);
            if (noReplyName == null)
                continue;

            if (contextType.FindImplementationForInterfaceMember(callback) is not IMethodSymbol implementation)
                continue;

            // An event nobody overrode is an event the MTA is not being asked for at all, so there is no
            // reply to spare it.
            if (implementation.Equals(callback, SymbolEqualityComparer.Default))
                continue;

            if (CommitActionTypeOf(callback) is not { } commitActionType)
                continue;

            if (AnswersOnlyUnobservably(implementation, commitActionType, compilation, semanticModels, cancellationToken))
                names.Add(noReplyName);
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToImmutableArray();
    }

    // The commit action type is read off the callback's own signature rather than named here, so the
    // generator still knows nothing about the action hierarchy.
    private static ITypeSymbol? CommitActionTypeOf(IMethodSymbol callback)
    {
        if (callback.ReturnType is not INamedTypeSymbol { TypeArguments.Length: 1 } returnType)
            return null;

        return returnType.TypeArguments[0];
    }

    private static bool AnswersOnlyUnobservably(
        IMethodSymbol implementation,
        ITypeSymbol commitActionType,
        Compilation compilation,
        Dictionary<SyntaxTree, SemanticModel> semanticModels,
        CancellationToken cancellationToken)
    {
        if (implementation.DeclaringSyntaxReferences.Length == 0)
            return false;

        // A body only proves anything if it is the body that will run. An overridable callback on an
        // unsealed context can be replaced by a derived type answering something else entirely - which
        // is not hypothetical: a partial mock is exactly that, and so is any filter written as a base
        // class with hooks.
        if (CanBeOverridden(implementation))
            return false;

        var mentionedAny = false;

        foreach (var reference in implementation.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax(cancellationToken);

            // The body only. A declaration also carries its return type, and ValueTask<CommitAction>
            // mentions the commit action base - which is not unobservable, so walking the whole
            // declaration proves every callback guilty of being able to decide.
            if (BodyOf(node) is not { } body)
                return false;

            var model = GetSemanticModel(compilation, semanticModels, body.SyntaxTree);

            foreach (var expression in body.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var type = model.GetTypeInfo(expression, cancellationToken).Type;

                if (type == null || !IsCommitAction(type, commitActionType))
                    continue;

                mentionedAny = true;

                if (!MilterUnobservableActionMarkerAttribute.Describes(type))
                    return false;
            }
        }

        return mentionedAny;
    }

    private static bool CanBeOverridden(IMethodSymbol implementation)
    {
        if (implementation.IsAbstract)
            return true;

        if (implementation.IsSealed || implementation.ContainingType.IsSealed)
            return false;

        return implementation.IsVirtual || implementation.IsOverride;
    }

    private static SyntaxNode? BodyOf(SyntaxNode declaration)
    {
        if (declaration is not MethodDeclarationSyntax method)
            return null;

        return (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression;
    }

    private static bool IsCommitAction(ITypeSymbol type, ITypeSymbol commitActionType)
    {
        for (var candidate = type; candidate != null; candidate = candidate.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, commitActionType))
                return true;
        }

        return false;
    }

    private static SemanticModel GetSemanticModel(Compilation compilation, Dictionary<SyntaxTree, SemanticModel> cache, SyntaxTree tree)
    {
        if (cache.TryGetValue(tree, out var model))
            return model;

        model = compilation.GetSemanticModel(tree);
        cache[tree] = model;

        return model;
    }
}
