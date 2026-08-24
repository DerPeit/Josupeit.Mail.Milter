// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Diagnostics.CodeAnalysis;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The lowest Milter protocol version an MTA must speak before this filter can do its job.
/// </summary>
/// <remarks>
/// <para>
/// A closed set rather than a <see cref="System.Version"/>, because the protocol has no version 5 -
/// Sendmail went from 4 to 6 - and a type that can represent one would be describing something that
/// does not exist.
/// </para>
/// <para>
/// The source generator works this out from what the filter actually uses, so a filter that uses
/// nothing newer than version 2 declares nothing and loses nothing. Negotiation fails when the MTA
/// offers less than this, which is what turns a capability the MTA silently withheld into a failure on
/// the first connection instead of a filter quietly not working.
/// </para>
/// </remarks>
[ProtocolVersionRequirementMarker]
[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "The members are the wire version numbers and the protocol has no version 0. A zero member would be a version no MTA can speak.")]
public enum ProtocolVersionRequirement
{
    /// <summary>
    /// Everything this filter uses has existed since version 2, so any MTA will do.
    /// </summary>
    Version2 = 2,

    /// <summary>
    /// Needs version 3, which added the unrecognised-command event.
    /// </summary>
    Version3 = 3,

    /// <summary>
    /// Needs version 4, which added the <c>DATA</c> event.
    /// </summary>
    Version4 = 4,

    /// <summary>
    /// Needs version 6, which added skipping, the no-reply family, the delivery changes and the
    /// version 6 modifications.
    /// </summary>
    Version6 = 6,
}
