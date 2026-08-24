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
/// implementation only overrides the events it cares about. The six properties here are what the library
/// tells the MTA during negotiation. Two of them have no default, because no default is safe; the other
/// four behave exactly as protocol version 2 did until something says otherwise, and the source
/// generator writes all six from the filter's own code unless the filter declares one by hand.
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

    /// <summary>
    /// Changes this filter wants to how the MTA delivers events.
    /// </summary>
    /// <remarks>
    /// Unlike the two properties above this one has a safe default, so it is
    /// <see langword="virtual"/> rather than abstract: <see cref="DeliveryRequirements.None"/> is what
    /// every version 2 filter has always negotiated, and a filter that never declares it behaves exactly
    /// as it did. The source generator still writes it when it is left alone, deriving
    /// <see cref="DeliveryRequirements.RejectedRecipients"/> from whether the code reads
    /// <see cref="RecipientInformation.IsRejectedByMta"/>.
    /// </remarks>
    static virtual DeliveryRequirements DeliveryRequirements => DeliveryRequirements.None;

    /// <summary>
    /// The events this filter promises not to answer.
    /// </summary>
    /// <remarks>
    /// Left to the source generator, which sets an event here only once it has proved the callback can
    /// answer nothing but <see cref="CommitActions.Continue"/> - so declaring it by hand is rarely
    /// needed, and is a promise the library will hold the filter to.
    /// </remarks>
    static virtual NoReplyRequirements NoReplyRequirements => NoReplyRequirements.None;

    /// <summary>
    /// The lowest protocol version an MTA must speak for this filter to work.
    /// </summary>
    /// <remarks>
    /// Left to the source generator, which folds it out of what the other properties turned out to need.
    /// Negotiation fails when the MTA offers less, which turns a capability silently withheld into a
    /// failure on the first connection.
    /// </remarks>
    static virtual ProtocolVersionRequirement ProtocolVersionRequirement => ProtocolVersionRequirement.Version2;

    /// <summary>
    /// The macros this filter wants, per point in the conversation.
    /// </summary>
    /// <remarks>
    /// Left to the source generator, which reads which names the macro callback looks up and under which
    /// stage. A read it cannot pin down is reported rather than guessed, because a list that leaves a
    /// macro out fails nowhere near where it was caused - the filter simply finds the key missing at
    /// runtime. <see cref="MacroRequirements.None"/> asks for no list at all, which is the behaviour
    /// that predates this.
    /// </remarks>
    static virtual MacroRequirements MacroRequirements => MacroRequirements.None;
}
