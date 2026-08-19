// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// A set of macro values the MTA supplied ahead of an event.
/// </summary>
/// <remarks>
/// Macros are how the MTA passes on what it knows but the SMTP conversation does not say - the queue id,
/// the authenticated user, the result of its own checks. Which ones arrive is the MTA's configuration to
/// decide, so a filter should treat any given macro as possibly absent.
/// </remarks>
/// <param name="ProtocolStage">The event these values were sent for.</param>
/// <param name="Macros">The values, keyed by macro name.</param>
public readonly record struct MacroDefinitionInformation(
    [property: MilterMacroSourceMarker(isStage: true)] ProtocolStage ProtocolStage,
    [property: MilterMacroSourceMarker] IReadOnlyDictionary<string, string> Macros);