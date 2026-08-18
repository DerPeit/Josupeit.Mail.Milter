// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.UnitTests;

internal sealed record GeneratorRun(
    string[] GeneratedSources,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics)
{
    public string GeneratedSource => GeneratedSources.SingleOrDefault() ?? string.Empty;

    public string[] Errors =>
    [
        .. CompilationDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .Distinct(),
    ];

    public string[] GeneratorErrorIds =>
    [
        .. GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id),
    ];
}
