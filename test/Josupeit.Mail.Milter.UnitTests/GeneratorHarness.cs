// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Josupeit.Mail.Milter.Generator;

namespace Josupeit.Mail.Milter.UnitTests;

// Runs MilterContextSourceGenerator over a snippet the way the compiler would. The result carries the
// generated text, the generator's own diagnostics, and the diagnostics of compiling the snippet together
// with what was generated - the last of those is what proves the emitted code is not merely plausible.
internal static class GeneratorHarness
{
    private static readonly IReadOnlyList<MetadataReference> _references = ResolveReferences();

    public static GeneratorRun Run(params string[] sources)
    {
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)));

        var compilation = CSharpCompilation.Create(
            assemblyName: "Filter",
            syntaxTrees: trees,
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new MilterContextSourceGenerator());

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        // The driver appends what it generated to the trees it was given, in that order.
        var generated = output.SyntaxTrees
            .Skip(sources.Length)
            .Select(tree => tree.ToString())
            .ToArray();

        return new GeneratorRun(generated, generatorDiagnostics, output.GetDiagnostics());
    }

    private static IReadOnlyList<MetadataReference> ResolveReferences()
    {
        var platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        var paths = platform
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Concat(Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return paths
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
