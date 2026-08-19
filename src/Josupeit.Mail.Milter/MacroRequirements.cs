// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The macros this filter wants, per point in the SMTP conversation.
/// </summary>
/// <remarks>
/// <para>
/// The MTA sends a configured set of macros before each event whether the filter reads them or not.
/// Naming the ones actually wanted (<c>SMFIF_SETSYMLIST</c>) trims that to what is used - which needs
/// an MTA speaking protocol version 6.
/// </para>
/// <para>
/// <see cref="None"/> means no list is sent at all, and the MTA falls back to its own configuration.
/// That is the safe answer and the one a filter gets when it declares nothing: fewer macros is an
/// optimisation, so failing to ask for it costs bandwidth rather than correctness.
/// </para>
/// </remarks>
[MacroRequirementsMarker]
public sealed record MacroRequirements
{
    /// <summary>
    /// No list at all. The MTA sends whatever it is configured to send.
    /// </summary>
    public static readonly MacroRequirements None = new(ImmutableDictionary<ProtocolStage, IReadOnlyList<string>>.Empty);

    private readonly ImmutableDictionary<ProtocolStage, IReadOnlyList<string>> _byStage;

    private MacroRequirements(ImmutableDictionary<ProtocolStage, IReadOnlyList<string>> byStage)
    {
        _byStage = byStage;
    }

    /// <summary>
    /// The macro names wanted at each stage.
    /// </summary>
    public IReadOnlyDictionary<ProtocolStage, IReadOnlyList<string>> ByStage => _byStage;

    /// <summary>
    /// Whether no macros are named at all, so no list is sent.
    /// </summary>
    public bool IsEmpty => _byStage.IsEmpty;

    /// <summary>
    /// Names the macros wanted at one stage.
    /// </summary>
    /// <param name="stage">The point in the conversation the macros are wanted at.</param>
    /// <param name="macroNames">
    /// The macro names, spelled as the MTA spells them - <c>{mail_addr}</c> with its braces, <c>i</c>
    /// without.
    /// </param>
    /// <returns>The requirements, to add further stages to with <see cref="And"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="stage"/> has no macro class in the protocol, or a name is empty, contains a
    /// control character, or contains a space.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="macroNames"/> is
    /// <see langword="null"/>.</exception>
    public static MacroRequirements For(ProtocolStage stage, params string[] macroNames)
    {
        return None.And(stage, macroNames);
    }

    /// <summary>
    /// Names the macros wanted at a further stage.
    /// </summary>
    /// <param name="stage">The point in the conversation the macros are wanted at.</param>
    /// <param name="macroNames">The macro names, spelled as the MTA spells them.</param>
    /// <returns>The requirements, including the stage just added.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="stage"/> has no macro class in the protocol, or a name is empty, contains a
    /// control character, or contains a space.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="macroNames"/> is
    /// <see langword="null"/>.</exception>
    public MacroRequirements And(ProtocolStage stage, params string[] macroNames)
    {
        if (macroNames == null)
            throw new ArgumentNullException(nameof(macroNames));

        GetMacroStageCode(stage);

        foreach (var macroName in macroNames)
        {
            ArgumentException.ThrowIfNullOrEmpty(macroName, nameof(macroNames));
            ArgumentException.ThrowIfContainsControlCharacter(macroName, nameof(macroNames));

            // The wire carries one stage's names as a single space-separated string, so a name with a
            // space in it silently becomes two names the MTA will never match.
            if (macroName.Contains(' '))
                throw new ArgumentException("A macro name must not contain a space.", nameof(macroNames));
        }

        var merged = _byStage.TryGetValue(stage, out var existing)
            ? existing.Concat(macroNames).Distinct(StringComparer.Ordinal).ToArray()
            : macroNames.Distinct(StringComparer.Ordinal).ToArray();

        return new MacroRequirements(_byStage.SetItem(stage, merged));
    }

    /// <summary>
    /// The protocol's macro class for a stage (<c>SMFIM_</c>).
    /// </summary>
    /// <remarks>
    /// Deliberately not the same numbers as <see cref="ProtocolStage"/>, which runs in conversation
    /// order. <see cref="ProtocolStage.Header"/> and <see cref="ProtocolStage.Body"/> have no macro
    /// class at all, so no list can be asked for at either.
    /// </remarks>
    /// <param name="stage">The stage to translate.</param>
    /// <returns>The <c>SMFIM_</c> code.</returns>
    /// <exception cref="ArgumentException">The stage has no macro class.</exception>
    public static uint GetMacroStageCode(ProtocolStage stage)
    {
        return stage switch
        {
            ProtocolStage.Connect => 0,
            ProtocolStage.ClientHello => 1,
            ProtocolStage.Sender => 2,
            ProtocolStage.Recipient => 3,
            ProtocolStage.MessageData => 4,
            ProtocolStage.EndOfBody => 5,
            ProtocolStage.EndOfHeaders => 6,
            _ => throw new ArgumentException($"The protocol defines no macro class for {stage}.", nameof(stage)),
        };
    }
}
