// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Josupeit.Mail.Milter.Generator;

// The traversal both requirement analyses run: start at some declarations, look at every expression in
// them, and follow whatever they mention into its own declarations, with a visited set so a cycle ends.
//
// Only the mechanism lives here. What an analysis collects from an expression, which symbols it follows,
// and what it does when it loses track are all the caller's, and deliberately so: one analysis
// over-approximates and reports when it cannot see far enough, the other under-approximates and stays
// quiet, and that difference is the whole safety argument for the pair. Sharing the queue is safe
// because a work list has no opinion; sharing the policy would not be.
internal sealed class DeclarationWalk
{
    private readonly Compilation _compilation;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModels = [];
    private readonly HashSet<ISymbol> _visitedSymbols = new(SymbolEqualityComparer.Default);
    private readonly HashSet<SyntaxNode> _enqueuedNodes = [];
    private readonly Queue<SyntaxNode> _pending = new();

    private bool _isStopped;

    public DeclarationWalk(Compilation compilation, CancellationToken cancellationToken)
    {
        _compilation = compilation;
        _cancellationToken = cancellationToken;
    }

    // Queues everything the symbol is declared by. A partial type has several declarations and all of
    // them count, which is why this takes the symbol rather than a single node.
    public void Enqueue(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax(_cancellationToken);

            if (_enqueuedNodes.Add(node))
                _pending.Enqueue(node);
        }
    }

    // True the first time a symbol is offered, false ever after. What the caller does with a repeat is
    // its own business; all this promises is that the walk will not loop.
    public bool IsFirstVisit(ISymbol symbol)
    {
        return _visitedSymbols.Add(symbol);
    }

    // Ends the walk early. The may-analysis uses it the moment it loses track: once it has to report,
    // nothing later can change the answer, so there is no point reading the rest.
    public void Stop()
    {
        _isStopped = true;
    }

    public void Run(Action<ExpressionSyntax, SemanticModel> visit)
    {
        if (visit == null)
            throw new ArgumentNullException(nameof(visit));

        while (_pending.Count > 0 && !_isStopped)
        {
            var node = _pending.Dequeue();
            var model = GetSemanticModel(node.SyntaxTree);

            foreach (var expression in node.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (_isStopped)
                    return;

                visit(expression, model);
            }
        }
    }

    // One model per tree, kept because asking the compilation for one is not cheap and a walk revisits
    // the same tree once per declaration it holds.
    private SemanticModel GetSemanticModel(SyntaxTree tree)
    {
        if (_semanticModels.TryGetValue(tree, out var model))
            return model;

        model = _compilation.GetSemanticModel(tree);
        _semanticModels[tree] = model;

        return model;
    }
}
