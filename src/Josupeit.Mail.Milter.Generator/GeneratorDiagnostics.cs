// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

// The diagnostics the generator reports. Both are errors on purpose: the two properties are static
// abstract members, so refusing to emit one already fails the build with CS0535, and a diagnostic that
// only warned would leave the author reading the wrong error.
internal static class GeneratorDiagnostics
{
    public static readonly DiagnosticDescriptor ContextIsNotPartial = new(
        id: "MILT001",
        title: "Milter context is not partial",
        messageFormat: "'{0}' implements IMilterContext but is not declared partial, so its requirement properties cannot be generated",
        category: "Josupeit.Mail.Milter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator emits the ProtocolRequirements and ActionRequirements properties into a second part of the class. Declare the class partial, or declare both properties by hand.");

    public static readonly DiagnosticDescriptor UntrackedAction = new(
        id: "MILT002",
        title: "Cannot determine the actions a Milter context may yield",
        messageFormat: "'{0}' obtains an action from '{1}', which this compilation has no body for, so ActionRequirements cannot be derived",
        category: "Josupeit.Mail.Milter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ActionRequirements is derived by following every action the context builds. An action arriving from an abstract member, an interface, or another assembly cannot be followed, and guessing would either under-declare the negotiation - which makes the MTA refuse the modification - or over-declare it. Declare 'static ActionRequirements ActionRequirements' by hand.");
}
