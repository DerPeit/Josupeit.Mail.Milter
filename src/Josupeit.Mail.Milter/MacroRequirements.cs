// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
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

        ArgumentException.ThrowIfInvalidMacroClass(stage);

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
    /// Whether another set names the same macros at the same stages, in the same order.
    /// </summary>
    /// <remarks>
    /// Written by hand because the record's generated equality would compare the dictionary behind these
    /// by reference, so two sets built from identical calls would come out unequal - a record that
    /// advertises value semantics and does not have them. Order counts within a stage: the names travel
    /// as one space-separated string, so two orders are two different strings on the wire.
    /// </remarks>
    /// <param name="other">The set to compare with.</param>
    /// <returns><see langword="true"/> when both name exactly the same macros.</returns>
    public bool Equals(MacroRequirements? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_byStage.Count != other._byStage.Count)
            return false;

        foreach (var (stage, macroNames) in _byStage)
        {
            if (!other._byStage.TryGetValue(stage, out var otherMacroNames))
                return false;

            if (!macroNames.SequenceEqual(otherMacroNames, StringComparer.Ordinal))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = 0;

        // Combined with exclusive-or across stages because a dictionary has no defined enumeration
        // order, and a hash that depended on it would differ between two sets that compare equal.
        foreach (var (stage, macroNames) in _byStage)
        {
            var stageHash = (int)stage;

            foreach (var macroName in macroNames)
                stageHash = (stageHash * 31) + StringComparer.Ordinal.GetHashCode(macroName);

            hash ^= stageHash;
        }

        return hash;
    }
}

// Guards the stages a macro list can be asked for. The protocol gives most of them a macro class
// (SMFIM_) but not all: Header and Body have none, and asking for one there would put a stage code on
// the wire that means something else. Only the guard lives here - the codes themselves belong to the
// frame factory, which is the one place that writes them, so nothing outside it has to know that the
// numbers are not ProtocolStage's own.
file static class MacroClass
{
    extension(ArgumentException)
    {
        public static void ThrowIfInvalidMacroClass(ProtocolStage stage, [CallerArgumentExpression(nameof(stage))] string? paramName = null)
        {
            var hasMacroClass = stage
                is ProtocolStage.Connect
                or ProtocolStage.ClientHello
                or ProtocolStage.Sender
                or ProtocolStage.Recipient
                or ProtocolStage.MessageData
                or ProtocolStage.EndOfHeaders
                or ProtocolStage.EndOfBody;

            if (!hasMacroClass)
                throw new ArgumentException($"The protocol defines no macro class for {stage}.", paramName);
        }
    }
}
