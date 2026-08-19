// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Changes to how the MTA delivers events, declared to it when the connection is negotiated.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="ProtocolRequirements"/>, which asks for fewer events, these ask for
/// <em>different</em> ones - more recipients in one case, differently spelled header values in the
/// other. Both need an MTA speaking protocol version 6.
/// </para>
/// <para>
/// The source generator derives <see cref="RejectedRecipients"/> on its own, from whether the filter
/// reads <see cref="RecipientInformation.IsRejectedByMta"/>. It can never derive
/// <see cref="LeadingHeaderSpace"/>, because nothing in a filter's code distinguishes one that wants
/// byte-exact header values from one that does not - so a filter that wants it declares this property
/// by hand, and then has to name <see cref="RejectedRecipients"/> itself as well.
/// </para>
/// </remarks>
[Flags]
[DeliveryRequirementsMarker]
public enum DeliveryRequirements
{
    /// <summary>
    /// The MTA delivers events as it always has.
    /// </summary>
    None = 0,

    /// <summary>
    /// Header values keep the space that follows the colon (<c>SMFIP_HDR_LEADSPC</c>).
    /// </summary>
    /// <remarks>
    /// This cuts both ways, which is the point of it: the value handed to
    /// <see cref="IMilterCallbacks.OnHeader"/> keeps its leading space, and the MTA stops inserting one
    /// in front of values the filter writes. A filter doing byte-exact header work - re-signing DKIM,
    /// say - needs both halves or it changes the bytes it was trying to preserve.
    /// </remarks>
    LeadingHeaderSpace = 1,

    /// <summary>
    /// Recipients the MTA has already rejected are delivered too (<c>SMFIP_RCPT_REJ</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="IMilterCallbacks.OnRecipient"/> then fires for addresses the MTA is going to refuse
    /// anyway, with <see cref="RecipientInformation.IsRejectedByMta"/> set. Useful for a filter that
    /// counts what a client is attempting rather than what it achieves.
    /// </remarks>
    RejectedRecipients = 2,

    /// <summary>
    /// Every delivery change at once.
    /// </summary>
    All = LeadingHeaderSpace | RejectedRecipients,
}
