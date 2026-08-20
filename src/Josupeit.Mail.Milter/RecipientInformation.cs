// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using Josupeit.Mail.Milter.Generator.Markers;

namespace Josupeit.Mail.Milter;

/// <summary>
/// What the MTA reports about a <c>RCPT TO</c>.
/// </summary>
/// <remarks>
/// Deliberately a separate type from <see cref="SenderInformation"/>, which carries the same first two
/// members. Only a recipient can have been rejected by the MTA already, so one shared type would leave
/// <see cref="IsRejectedByMta"/> meaningless in half its uses.
/// </remarks>
/// <param name="AddressString">
/// The envelope address exactly as it appeared on the command, angle brackets included - so
/// <c>&lt;user@example.com&gt;</c> rather than <c>user@example.com</c>. It is the raw argument and not a
/// parsed address, so a filter that needs the parts has to parse it.
/// </param>
/// <param name="ESmtpArguments">
/// The ESMTP parameters that followed the address, such as <c>NOTIFY=</c> or <c>ORCPT=</c>, each as its
/// own unparsed string. Empty when the command carried none.
/// </param>
/// <param name="IsRejectedByMta">
/// Whether the MTA has already decided to refuse this recipient.
/// <para>
/// Only ever <see langword="true"/> when <see cref="DeliveryRequirements.RejectedRecipients"/> was
/// negotiated; without it the MTA never offers a rejected recipient at all, so this is
/// <see langword="false"/> for every recipient a filter sees. Reading it is what makes the source
/// generator ask for the flag, so a filter that looks at it gets the events it is looking for.
/// </para>
/// <para>
/// Why the MTA refused is not carried here. It arrives in the recipient-stage macros -
/// <c>{rcpt_host}</c> holds the enhanced status code and <c>{rcpt_addr}</c> the reason - which
/// <see cref="IMilterCallbacks.OnDefineMacro"/> receives just before this event.
/// </para>
/// </param>
public readonly record struct RecipientInformation(
    string AddressString,
    IReadOnlyList<string> ESmtpArguments,
    [property: MilterDeliveryMarker(nameof(DeliveryRequirements.RejectedRecipients))] bool IsRejectedByMta);
