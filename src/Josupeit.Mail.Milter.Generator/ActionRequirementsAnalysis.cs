// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Josupeit.Mail.Milter.Generator;

// Works out which modifications a context can perform by reading the code that builds them.
//
// This is a may-analysis, and that is what makes it tractable: it takes the union of every action the
// code mentions and never asks whether a branch is reachable. So an action behind an if, in a loop, in a
// catch block or in a lambda counts exactly like one on the straight path, and no control flow has to be
// understood at all. The exact set a filter yields at runtime is not computable; a superset is, and a
// superset is what the negotiation needs.
//
// What does bound it is whose code can be read. The context type, its base types and every method they
// call are followed as far as this compilation has bodies for them. The moment an action arrives from
// somewhere without one - another assembly, an interface, an abstract member - the analysis says so
// rather than guessing.
internal sealed class ActionRequirementsAnalysis
{
    private readonly DeclarationWalk _walk;
    private readonly HashSet<string> _requirementNames = new(StringComparer.Ordinal);

    private int _minimumProtocolVersion;
    private string? _untrackedSource;
    private Location? _untrackedLocation;

    private ActionRequirementsAnalysis(Compilation compilation, CancellationToken cancellationToken)
    {
        _walk = new DeclarationWalk(compilation, cancellationToken);
    }

    public static ActionRequirementsResult Run(INamedTypeSymbol contextType, Compilation compilation, CancellationToken cancellationToken)
    {
        var analysis = new ActionRequirementsAnalysis(compilation, cancellationToken);

        return analysis.Run(contextType);
    }

    // True when every action the member can hand back was handed to it first. Such a member moves
    // actions about rather than building them - ToAsyncEnumerable, Select, Concat, a list indexer - and
    // whatever it moves was written at the call site, where it has already been counted.
    //
    // This is judged on the substituted symbol, not the definition: ToAsyncEnumerable declares
    // IEnumerable<TSource>, and only the call site says that TSource is Action. It is also the one place
    // the analysis trades soundness for usability - a generic factory that builds an action from nothing
    // but a type argument reads as a pass-through here and is not caught.
    private static bool PassesActionsThrough(ISymbol symbol)
    {
        if (ActionTypes.MentionsAction(symbol.ContainingType))
            return true;

        if (symbol is not IMethodSymbol method)
            return false;

        return ActionTypes.MentionsAction(method.ReceiverType)
            || method.Parameters.Any(parameter => ActionTypes.MentionsAction(parameter.Type))
            || method.TypeArguments.Any(ActionTypes.MentionsAction);
    }

    private static ITypeSymbol? ReturnTypeOf(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.ReturnType,
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null,
        };
    }

    private ActionRequirementsResult Run(INamedTypeSymbol contextType)
    {
        Seed(contextType);

        _walk.Run(Visit);

        var names = _requirementNames.OrderBy(name => name, StringComparer.Ordinal).ToImmutableArray();

        return new ActionRequirementsResult(names, _minimumProtocolVersion, _untrackedSource, _untrackedLocation);
    }

    private void Seed(INamedTypeSymbol contextType)
    {
        for (var type = contextType; type != null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            if (type.DeclaringSyntaxReferences.Length > 0)
            {
                _walk.Enqueue(type);

                continue;
            }

            // A base class from another assembly can carry the whole of OnEndOfBody with it, and there is
            // no call site here to notice. Losing it silently is the one failure this analysis must not
            // have, so an inherited member that deals in actions ends it here.
            var inherited = type
                .GetMembers()
                .FirstOrDefault(member => ActionTypes.MayCarryUntrackedAction(ReturnTypeOf(member)));

            if (inherited != null)
                Lose(inherited, contextType.Locations.FirstOrDefault());

            return;
        }
    }

    private void Visit(ExpressionSyntax expression, SemanticModel model)
    {
        var expressionType = model.GetTypeInfo(expression).Type;

        var name = ActionTypes.RequirementNameOf(expressionType);
        if (name != null)
            _requirementNames.Add(name);

        // Tracked separately from the flag, because an action can need a protocol version without
        // needing a modification right - the skip action is exactly that.
        var version = ActionTypes.MinimumProtocolVersionOf(expressionType);
        if (version > _minimumProtocolVersion)
            _minimumProtocolVersion = version;

        var symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol != null)
            Follow(symbol, expression);
    }

    private void Follow(ISymbol symbol, ExpressionSyntax expression)
    {
        var returnType = ReturnTypeOf(symbol);

        // Anything whose type pins down a concrete action was already counted from the expression itself,
        // and anything that is not an action at all is none of this analysis's business.
        if (!ActionTypes.MayCarryUntrackedAction(returnType))
            return;

        var definition = symbol.OriginalDefinition;

        if (!_walk.IsFirstVisit(definition))
            return;

        if (!definition.IsAbstract && definition.DeclaringSyntaxReferences.Length > 0)
        {
            // A method is followed into its own body. A field or property is followed into its whole
            // declaring type, because what a field holds is decided wherever it is written, not where it
            // is declared.
            _walk.Enqueue(definition is IMethodSymbol ? definition : definition.ContainingType);

            return;
        }

        if (PassesActionsThrough(symbol))
            return;

        Lose(definition, expression.GetLocation());
    }

    // Ends the walk as well as recording what was lost. Once this analysis has to report, nothing later
    // can change the answer, so reading the rest would only cost time.
    private void Lose(ISymbol symbol, Location? location)
    {
        _untrackedSource = symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        _untrackedLocation = location;

        _walk.Stop();
    }
}
