using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

internal abstract class MethodVisitor : SymbolVisitor<IEnumerable<IMethodSymbol>>
{
    public override IEnumerable<IMethodSymbol> Visit(ISymbol? symbol)
    {
        if (symbol == null)
            return [];

        return base.Visit(symbol)!;
    }

    public override IEnumerable<IMethodSymbol> VisitAssembly(IAssemblySymbol symbol)
    {
        return symbol.Modules.SelectMany(Visit);
    }

    public override IEnumerable<IMethodSymbol> VisitModule(IModuleSymbol symbol)
    {
        return Visit(symbol.GlobalNamespace);
    }

    public override IEnumerable<IMethodSymbol> VisitNamespace(INamespaceSymbol symbol)
    {
        return symbol.GetMembers().SelectMany(Visit);
    }

    public override IEnumerable<IMethodSymbol> VisitNamedType(INamedTypeSymbol symbol)
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

    public override IEnumerable<IMethodSymbol> VisitMethod(IMethodSymbol symbol)
    {
        return [symbol];
    }

    public override IEnumerable<IMethodSymbol> DefaultVisit(ISymbol symbol)
    {
        yield break;
    }
}