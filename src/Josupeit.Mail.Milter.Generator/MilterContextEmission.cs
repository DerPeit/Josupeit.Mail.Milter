// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

// Everything the generator decided about one context class, rendered down to strings. The analysis
// happens in the pipeline's transform, so what travels from there to RegisterSourceOutput has to be
// small and comparable - a symbol would pin a whole compilation alive and compare by reference.
internal sealed record MilterContextEmission(
    string HintName,
    string? Namespace,
    EquatableArray<string> ContainingTypes,
    string TypeDeclaration,
    EquatableArray<string> Properties,
    EquatableArray<Diagnostic> Diagnostics);
