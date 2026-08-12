// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

internal sealed class MethodLocator
{
    public IEnumerable<IMethodSymbol> LocateMethodsWith<TMarkerAttribute>(ISymbol root) where TMarkerAttribute : Attribute
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        var visitor = new MarkerAttributeVisitor<TMarkerAttribute>();

        return visitor.Visit(root);
    }
}

file sealed class MarkerAttributeVisitor<TMarkerAttribute> : MethodVisitor where TMarkerAttribute : Attribute
{
    public override IEnumerable<IMethodSymbol> VisitMethod(IMethodSymbol symbol)
    {
        if (symbol.HasAttribute<TMarkerAttribute>())
            return base.VisitMethod(symbol);

        return [];
    }
}
