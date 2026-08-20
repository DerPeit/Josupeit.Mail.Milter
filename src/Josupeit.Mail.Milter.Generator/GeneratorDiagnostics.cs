// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

// The diagnostics the generator reports. All three are errors on purpose, and MILT003 has to stay one.
//
// MILT001 and MILT002 have CS0535 behind them: the properties they guard are static abstract members,
// so refusing to emit one already fails the build and a warning would only leave the author reading the
// wrong error. MacroRequirements is static virtual and has no such net - downgrade MILT003 and a filter
// whose macro reads could not be followed silently negotiates the wrong list.
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

    public static readonly DiagnosticDescriptor UntrackedMacro = new(
        id: "MILT003",
        title: "Cannot determine the macros a Milter context reads",
        messageFormat: "'{0}' contains {1}, so MacroRequirements cannot be derived",
        category: "Josupeit.Mail.Milter",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "MacroRequirements is derived by reading which macro names the context looks up and at which protocol stage. A list that leaves one out does not fail at negotiation - the filter finds the key missing at runtime instead - so a read that cannot be pinned down is refused rather than guessed. Declare 'static MacroRequirements MacroRequirements' by hand; MacroRequirements.None asks for no list and leaves the MTA sending its configured set.");
}
