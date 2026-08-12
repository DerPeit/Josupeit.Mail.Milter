// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System;

namespace Josupeit.Mail.Milter.Generator;

internal sealed class PropertyLocator
{
    public IEnumerable<IPropertySymbol> LocatePropertiesOfType(ISymbol root, INamedTypeSymbol type)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        if (type == null)
            throw new ArgumentNullException(nameof(type));

        var visitor = new StaticPropertyTypeVisitor(type);

        return visitor.Visit(root);
    }
}

file sealed class StaticPropertyTypeVisitor(INamedTypeSymbol propertyType) : PropertyVisitor
{
    public override IEnumerable<IPropertySymbol> VisitProperty(IPropertySymbol symbol)
    {
        if (symbol.IsStatic && symbol.Type.Equals(propertyType, SymbolEqualityComparer.Default))
            return base.VisitProperty(symbol);

        return [];
    }
}
