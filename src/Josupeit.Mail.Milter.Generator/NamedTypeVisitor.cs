using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

internal abstract class NamedTypeVisitor : SymbolVisitor<IEnumerable<INamedTypeSymbol>>
{
    public override IEnumerable<INamedTypeSymbol> Visit(ISymbol? symbol)
    {
        if (symbol == null)
            return [];

        return base.Visit(symbol)!;
    }

    public override IEnumerable<INamedTypeSymbol> VisitAssembly(IAssemblySymbol symbol)
    {
        return symbol.Modules.SelectMany(Visit);
    }

    public override IEnumerable<INamedTypeSymbol> VisitModule(IModuleSymbol symbol)
    {
        return Visit(symbol.GlobalNamespace)!;
    }

    public override IEnumerable<INamedTypeSymbol> VisitNamespace(INamespaceSymbol symbol)
    {
        return symbol.GetMembers().SelectMany(Visit);
    }

    public override IEnumerable<INamedTypeSymbol> VisitNamedType(INamedTypeSymbol symbol)
    {
        return symbol.GetTypeMembers().SelectMany(Visit);
    }

    public override IEnumerable<INamedTypeSymbol> DefaultVisit(ISymbol symbol)
    {
        yield break;
    }
}