// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The contract a filter implements. Hand the implementing type to <see cref="Milter{TContext}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The callbacks come from <see cref="IMilterCallbacks"/> and all have default implementations, so an
/// implementation only overrides the events it cares about. The two properties here have none: they are
/// what the library tells the MTA during negotiation, and only the filter knows what to put in them.
/// </para>
/// <para>
/// An instance serves <em>one message</em>, not one connection. An MTA keeps a connection open across
/// several messages and the library builds a fresh context for each, so state kept in a field is
/// per-message state - which is usually what a filter wants, but it does mean nothing carries over.
/// </para>
/// </remarks>
[MilterContextMarker]
public interface IMilterContext : IMilterCallbacks
{
    /// <summary>
    /// The SMTP events this filter wants delivered.
    /// </summary>
    /// <remarks>
    /// Should match the callbacks actually overridden. Anything left out is never sent by the MTA, so its
    /// callback never runs.
    /// </remarks>
    static abstract ProtocolRequirements ProtocolRequirements { get; }

    /// <summary>
    /// The modifications this filter intends to perform.
    /// </summary>
    /// <remarks>
    /// The MTA refuses any modification that was not declared here, so this has to cover everything
    /// <see cref="IMilterCallbacks.OnEndOfBody"/> may yield.
    /// </remarks>
    static abstract ActionRequirements ActionRequirements { get; }
}
