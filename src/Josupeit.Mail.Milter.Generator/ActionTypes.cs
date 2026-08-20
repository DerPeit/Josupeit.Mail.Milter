// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using Josupeit.Mail.Milter.Generator.Markers;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

// Reads the MilterActionMarker attributes off a type. Everything the action analysis knows about the
// action hierarchy comes from here, so the generator never has to name Action or ModificationAction.
internal static class ActionTypes
{
    // The ActionRequirements member this type needs, or null when the type needs none - which covers
    // everything that is not a modification, the commit actions and the progress action included.
    public static string? RequirementNameOf(ITypeSymbol? type)
    {
        foreach (var candidate in SelfAndBaseTypes(type))
        {
            var name = MarkerOn(candidate) is { } marker
                ? MilterActionMarkerAttribute.GetActionRequirementName(marker)
                : null;

            if (name != null)
                return name;
        }

        return null;
    }

    // The lowest protocol version a value of this type needs from the MTA. Read off the nearest marker,
    // so an action that contributes no requirement flag - the skip action - still contributes a version.
    public static int MinimumProtocolVersionOf(ITypeSymbol? type)
    {
        foreach (var candidate in SelfAndBaseTypes(type))
        {
            if (MarkerOn(candidate) is { } marker)
                return MilterActionMarkerAttribute.GetMinimumProtocolVersion(marker);
        }

        return 0;
    }

    // True for an abstract type a modification can arrive disguised as - Action and ModificationAction.
    // CommitAction is abstract and marked through its base as well, but no type needing a flag derives
    // from it, so a value typed as one is never something the analysis has lost.
    public static bool IsUntrackedRoot(ITypeSymbol? type)
    {
        if (type is not { IsAbstract: true })
            return false;

        var marker = MarkerOn(type);

        return marker != null && MilterActionMarkerAttribute.GetActionRequirementName(marker) == null;
    }

    // True when the type is an action, or is generic over - or an array of - one. A signature that
    // mentions an action this way is one the analysis has to think about.
    public static bool MentionsAction(ITypeSymbol? type)
    {
        return Mentions(type, static candidate => SelfAndBaseTypes(candidate).Any(x => MarkerOn(x) != null));
    }

    // True when a value of this type could be a modification the analysis has not seen for itself.
    public static bool MayCarryUntrackedAction(ITypeSymbol? type)
    {
        return Mentions(type, IsUntrackedRoot);
    }

    private static bool Mentions(ITypeSymbol? type, Func<ITypeSymbol, bool> predicate)
    {
        switch (type)
        {
            case null:
                return false;

            case IArrayTypeSymbol array:
                return Mentions(array.ElementType, predicate);

            case INamedTypeSymbol named when named.TypeArguments.Any(argument => Mentions(argument, predicate)):
                return true;
        }

        return predicate(type);
    }

    private static AttributeData? MarkerOn(ITypeSymbol type)
    {
        return type
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass != null && MilterActionMarkerAttribute.IsDescribedBy(attribute.AttributeClass));
    }

    private static IEnumerable<ITypeSymbol> SelfAndBaseTypes(ITypeSymbol? type)
    {
        for (var candidate = type; candidate != null; candidate = candidate.BaseType)
            yield return candidate;
    }
}
