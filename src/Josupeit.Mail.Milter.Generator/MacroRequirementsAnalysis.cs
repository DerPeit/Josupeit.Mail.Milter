// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Josupeit.Mail.Milter.Generator;

// Works out which macros a filter reads, and at which point in the conversation, by reading the body of
// the macro callback.
//
// Strict on purpose, like the action analysis and unlike the other two. A macro list that leaves one out
// does not fail loudly: the filter simply finds the key missing at runtime, on some message, in
// production. So a read this cannot pin down to a constant name under a known stage reports MILT003 and
// emits nothing, and the author declares the property by hand. The escape hatch is one line, and it is
// the behaviour that predates any of this:
//
//     public static MacroRequirements MacroRequirements => MacroRequirements.None;
internal static class MacroRequirementsAnalysis
{
    public static MacroRequirementsResult Run(
        INamedTypeSymbol contextType,
        INamedTypeSymbol contextInterface,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var callback = contextInterface.AllInterfaces
            .Append(contextInterface)
            .SelectMany(@interface => @interface.GetMembers())
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method.Parameters.Any(parameter => CarriesMacroSource(parameter.Type)));

        if (callback == null)
            return MacroRequirementsResult.Empty;

        // Nobody reads macros, so nobody needs any. Not a failure - it is the commonest case by far.
        if (contextType.FindImplementationForInterfaceMember(callback) is not IMethodSymbol implementation
            || implementation.Equals(callback, SymbolEqualityComparer.Default))
        {
            return MacroRequirementsResult.Empty;
        }

        if (implementation.DeclaringSyntaxReferences.Length == 0)
            return MacroRequirementsResult.Lost("a macro callback with no body in this compilation", contextType.Locations.FirstOrDefault());

        var byStage = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var reference in implementation.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax(cancellationToken);
            var model = compilation.GetSemanticModel(node.SyntaxTree);

            foreach (var expression in node.DescendantNodes().OfType<ExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbol = model.GetSymbolInfo(expression, cancellationToken).Symbol;

                if (symbol == null || !MilterMacroSourceMarkerAttribute.DescribesMacros(symbol))
                    continue;

                // "info.Macros" is visited twice: once as the member access that stands for the read, and
                // once as the trailing identifier, which resolves to the same symbol but sits one level
                // too deep to read the key from. Only the outer one is the read.
                if (expression.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == expression)
                    continue;

                if (ReadNameOf(expression, model, cancellationToken) is not { } macroName)
                    return MacroRequirementsResult.Lost("a macro read whose name is not a constant", expression.GetLocation());

                if (StageOf(expression, model, cancellationToken) is not { } stage)
                    return MacroRequirementsResult.Lost("a macro read that no protocol stage guards", expression.GetLocation());

                if (!byStage.TryGetValue(stage, out var names))
                    byStage[stage] = names = new SortedSet<string>(StringComparer.Ordinal);

                names.Add(macroName);
            }
        }

        return MacroRequirementsResult.Found(byStage.ToImmutableDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.ToImmutableArray(),
            StringComparer.Ordinal));
    }

    private static bool CarriesMacroSource(ITypeSymbol type)
    {
        return type.GetMembers().Any(MilterMacroSourceMarkerAttribute.DescribesMacros);
    }

    // The constant key of an indexer read or a TryGetValue/ContainsKey call on the macro dictionary.
    // Anything else - a computed key, or the dictionary handed somewhere else entirely - has no name to
    // report, and that is what MILT003 is for.
    private static string? ReadNameOf(ExpressionSyntax macroAccess, SemanticModel model, CancellationToken cancellationToken)
    {
        var argument = macroAccess.Parent switch
        {
            ElementAccessExpressionSyntax elementAccess => elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression,
            MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax invocation } => invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression,
            _ => null,
        };

        if (argument == null)
            return null;

        var constant = model.GetConstantValue(argument, cancellationToken);

        return constant is { HasValue: true, Value: string name } ? name : null;
    }

    // The stage a read sits under, taken from the guard enclosing it. Only unambiguous shapes are
    // recognised: an if comparing the stage to a constant, a switch over it, a switch expression, and
    // the right-hand side of an && whose left-hand side compares the stage.
    private static string? StageOf(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (GuardingCondition(current) is { } condition
                && StageOfConjunction(condition, model, cancellationToken) is { } guarded)
            {
                return guarded;
            }

            if (current is SwitchSectionSyntax section
                && current.Parent is SwitchStatementSyntax switchStatement
                && IsStageExpression(switchStatement.Expression, model, cancellationToken))
            {
                return StageOfLabels(section, model, cancellationToken);
            }

            if (current is SwitchExpressionArmSyntax arm
                && current.Parent is SwitchExpressionSyntax switchExpression
                && IsStageExpression(switchExpression.GoverningExpression, model, cancellationToken))
            {
                return StageOfPattern(arm.Pattern, model, cancellationToken);
            }
        }

        return null;
    }

    // The condition known to hold whenever the node runs: an if's condition for its body, and the left
    // operand of && for its right one. The second is what lets the lookup share the if's condition -
    // "stage == EndOfBody && info.Macros.TryGetValue(...)" is the shape C# leads to.
    private static ExpressionSyntax? GuardingCondition(SyntaxNode node)
    {
        return node.Parent switch
        {
            IfStatementSyntax ifStatement when ifStatement.Statement == node => ifStatement.Condition,
            BinaryExpressionSyntax conjunction when conjunction.IsKind(SyntaxKind.LogicalAndExpression) && conjunction.Right == node => conjunction.Left,
            _ => null,
        };
    }

    // A condition holds only when every operand of its && chain does, so a stage comparison anywhere in
    // the chain pins the stage. Two comparisons naming different stages can never both hold, and are
    // refused rather than resolved in favour of either.
    private static string? StageOfConjunction(ExpressionSyntax condition, SemanticModel model, CancellationToken cancellationToken)
    {
        while (condition is ParenthesizedExpressionSyntax parenthesized)
            condition = parenthesized.Expression;

        if (condition is not BinaryExpressionSyntax binary || !binary.IsKind(SyntaxKind.LogicalAndExpression))
            return StageOfCondition(condition, model, cancellationToken);

        var left = StageOfConjunction(binary.Left, model, cancellationToken);
        var right = StageOfConjunction(binary.Right, model, cancellationToken);

        if (left != null && right != null)
            return string.Equals(left, right, StringComparison.Ordinal) ? left : null;

        return left ?? right;
    }

    private static string? StageOfCondition(ExpressionSyntax condition, SemanticModel model, CancellationToken cancellationToken)
    {
        if (condition is not BinaryExpressionSyntax binary || !binary.IsKind(SyntaxKind.EqualsExpression))
            return null;

        if (IsStageExpression(binary.Left, model, cancellationToken))
            return StageNameOf(binary.Right, model, cancellationToken);

        return IsStageExpression(binary.Right, model, cancellationToken)
            ? StageNameOf(binary.Left, model, cancellationToken)
            : null;
    }

    // One label means one stage. Several would put the read at every one of them, which over-asks rather
    // than merely guessing, so it is refused instead.
    private static string? StageOfLabels(SwitchSectionSyntax section, SemanticModel model, CancellationToken cancellationToken)
    {
        var names = section.Labels
            .OfType<CaseSwitchLabelSyntax>()
            .Select(label => StageNameOf(label.Value, model, cancellationToken))
            .Where(name => name != null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return names.Length == 1 ? names[0] : null;
    }

    private static string? StageOfPattern(PatternSyntax pattern, SemanticModel model, CancellationToken cancellationToken)
    {
        return pattern is ConstantPatternSyntax constant ? StageNameOf(constant.Expression, model, cancellationToken) : null;
    }

    private static bool IsStageExpression(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(expression, cancellationToken).Symbol;

        return symbol != null && MilterMacroSourceMarkerAttribute.DescribesStage(symbol);
    }

    private static string? StageNameOf(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(expression, cancellationToken).Symbol;

        return symbol is IFieldSymbol { IsConst: true, ContainingType.TypeKind: TypeKind.Enum } member ? member.Name : null;
    }
}
