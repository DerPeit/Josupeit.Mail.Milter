using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

internal abstract class PropertyVisitor : SymbolVisitor<IEnumerable<IPropertySymbol>> 
{
    public override IEnumerable<IPropertySymbol> Visit(ISymbol? symbol)
    {
        if (symbol == null)
            return [];

        return base.Visit(symbol)!;
    }

    public override IEnumerable<IPropertySymbol> VisitAssembly(IAssemblySymbol symbol)
    {
        return symbol.Modules.SelectMany(Visit);
    }

    public override IEnumerable<IPropertySymbol> VisitModule(IModuleSymbol symbol)
    {
        return Visit(symbol.GlobalNamespace);
    }

    public override IEnumerable<IPropertySymbol> VisitNamespace(INamespaceSymbol symbol)
    {
        return symbol.GetMembers().SelectMany(Visit);
    }

    public override IEnumerable<IPropertySymbol> VisitNamedType(INamedTypeSymbol symbol)
    {
        var result = symbol.GetMembers().SelectMany(Visit);

        switch (symbol.TypeKind)
        {
            case TypeKind.Interface:
                return result.Concat(symbol.Interfaces.SelectMany(Visit));

            case TypeKind.Class when symbol.BaseType is not null:
                return result.Concat(Visit(symbol.BaseType));

            default:
                return result;
        }
    }

    public override IEnumerable<IPropertySymbol> VisitProperty(IPropertySymbol symbol)
    {
        return [symbol];
    }

    public override IEnumerable<IPropertySymbol> DefaultVisit(ISymbol symbol)
    {
        yield break;
    }
}