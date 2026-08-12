// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

internal sealed class TypeLocator
{
    public IEnumerable<INamedTypeSymbol> LocateEnumsWith<TMarkerAttribute>(ISymbol root) where TMarkerAttribute : Attribute
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        var visitor = new MarkerAttributeVisitor<TMarkerAttribute>(TypeKind.Enum);

        return visitor.Visit(root)!;
    }

    public IEnumerable<INamedTypeSymbol> LocateInterfacesWith<TMarkerAttribute>(ISymbol root) where TMarkerAttribute : Attribute
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        var visitor = new MarkerAttributeVisitor<TMarkerAttribute>(TypeKind.Interface);

        return visitor.Visit(root)!;
    }

    public IEnumerable<INamedTypeSymbol> LocateClassesImplementingInterface(ISymbol root, INamedTypeSymbol @interface)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        if (@interface == null)
            throw new ArgumentNullException(nameof(@interface));

        var visitor = new InterfaceVisitor(@interface);

        return visitor.Visit(root)!;
    }
}

file sealed class MarkerAttributeVisitor<TMarkerAttribute>(TypeKind typeKind) : NamedTypeVisitor where TMarkerAttribute : Attribute
{
    public override IEnumerable<INamedTypeSymbol> VisitNamedType(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != typeKind)
            return base.VisitNamedType(symbol);

        if (!symbol.HasAttribute<TMarkerAttribute>())
            return base.VisitNamedType(symbol);

        return [symbol];
    }
}

file sealed class InterfaceVisitor(INamedTypeSymbol @interface) : NamedTypeVisitor
{
    public override IEnumerable<INamedTypeSymbol> VisitNamedType(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Class)
            return base.VisitNamedType(symbol);

        if (!symbol.AllInterfaces.Any(x => x.Equals(@interface, SymbolEqualityComparer.Default)))
            return base.VisitNamedType(symbol);

        return [symbol];
    }
}