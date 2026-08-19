// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter.Link;

// Which negotiation flags each protocol version defines, and the clamp that picks the version to answer
// with.
//
// The MTA refuses to send events that are not defined for the negotiated version, and libmilter may drop
// the connection when it receives one it does not expect, so a flag has to be masked away rather than
// merely unused. This table is the one place that mapping lives, which is what makes adding the next
// version's flag a local change instead of a hunt.
//
// The arms are written out rather than derived as "every bit below N": SMFIP_NR_HDR is 1<<7, a version 6
// flag sitting inside the version 2 range, so no contiguous rule describes the sets. They also
// deliberately avoid the All composites, which mean "everything this library knows" and would silently
// widen the version 2 arm the day a version 6 member is added to either enum.
internal static class ProtocolVersionCapabilities
{
    // The floor. The packet set, the parser and the state machine are all written against version 2, so
    // answering anything lower would be a claim this library cannot honour.
    internal static readonly Version Lowest = new(2, 0);

    // The ceiling, and a property of this library rather than of any one filter. Raising it means
    // teaching the arms below what the new version adds.
    internal static readonly Version Highest = new(6, 0);

    private static readonly Version _version3 = new(3, 0);
    private static readonly Version _version4 = new(4, 0);

    private const ContentBypassDiscriminator VERSION_2_CONTENT_BYPASS =
        ContentBypassDiscriminator.NoConnect
        | ContentBypassDiscriminator.NoHelo
        | ContentBypassDiscriminator.NoSender
        | ContentBypassDiscriminator.NoRecipient
        | ContentBypassDiscriminator.NoBody
        | ContentBypassDiscriminator.NoHeader
        | ContentBypassDiscriminator.NoEndOfHeaders;

    private const ContentBypassDiscriminator VERSION_3_CONTENT_BYPASS =
        VERSION_2_CONTENT_BYPASS | ContentBypassDiscriminator.NoUnknownCommand;

    private const ContentBypassDiscriminator VERSION_4_CONTENT_BYPASS =
        VERSION_3_CONTENT_BYPASS | ContentBypassDiscriminator.NoMessageData;

    private const ContentBypassDiscriminator VERSION_6_CONTENT_BYPASS =
        VERSION_4_CONTENT_BYPASS
        | ContentBypassDiscriminator.SkipRemainingEvents
        | ContentBypassDiscriminator.RejectedRecipients
        | ContentBypassDiscriminator.LeadingHeaderSpace
        | ContentBypassDiscriminator.NoReplyToConnect
        | ContentBypassDiscriminator.NoReplyToClientHello
        | ContentBypassDiscriminator.NoReplyToSender
        | ContentBypassDiscriminator.NoReplyToRecipient
        | ContentBypassDiscriminator.NoReplyToMessageData
        | ContentBypassDiscriminator.NoReplyToUnknownCommand
        | ContentBypassDiscriminator.NoReplyToHeader
        | ContentBypassDiscriminator.NoReplyToEndOfHeaders
        | ContentBypassDiscriminator.NoReplyToBody;

    // Quarantine arrived with Sendmail 8.13 rather than with a protocol version, and this library has
    // announced it against a version 2 MTA since it was written. Postfix validates the action word
    // against what it offered rather than against the version, so leaving it in the version 2 arm keeps
    // that working instead of silently withdrawing a capability.
    private const ActionDiscriminator VERSION_2_ACTIONS =
        ActionDiscriminator.AddHeaders
        | ActionDiscriminator.ChangeBodyChunks
        | ActionDiscriminator.AddRecipients
        | ActionDiscriminator.DeleteRecipients
        | ActionDiscriminator.ChangeOrDeleteHeaders
        | ActionDiscriminator.QuarantineMessage;

    private const ActionDiscriminator VERSION_6_ACTIONS =
        VERSION_2_ACTIONS
        | ActionDiscriminator.ChangeSender
        | ActionDiscriminator.AddRecipientsWithParameters
        | ActionDiscriminator.SetMacroList;

    // The version to answer with: the highest this library implements that the MTA also offers.
    //
    // Never higher than the offer, because Postfix rejects a filter announcing a version above its own.
    // Never higher than Highest, because the rest of this library would not honour it. And never a
    // version the protocol does not define - there is no version 5, so an MTA offering one is answered
    // with 4.
    public static Version Clamp(Version offeredVersion)
    {
        if (offeredVersion == null)
            throw new ArgumentNullException(nameof(offeredVersion));

        if (offeredVersion < Lowest)
            throw new NotSupportedException(
                $"The MTA offers Milter protocol version {offeredVersion}. This Milter implements versions " +
                $"{Lowest.Major} to {Highest.Major}, and cannot negotiate below {Lowest.Major}. Upgrade the MTA and retry.");

        if (offeredVersion >= Highest)
            return Highest;

        if (offeredVersion >= _version4)
            return _version4;

        if (offeredVersion >= _version3)
            return _version3;

        return Lowest;
    }

    // The events a filter may name at this version. Anything outside it must be dropped rather than sent.
    public static ContentBypassDiscriminator ContentBypassFor(Version negotiatedVersion)
    {
        if (negotiatedVersion == null)
            throw new ArgumentNullException(nameof(negotiatedVersion));

        if (negotiatedVersion >= Highest)
            return VERSION_6_CONTENT_BYPASS;

        if (negotiatedVersion >= _version4)
            return VERSION_4_CONTENT_BYPASS;

        if (negotiatedVersion >= _version3)
            return VERSION_3_CONTENT_BYPASS;

        return VERSION_2_CONTENT_BYPASS;
    }

    // The modifications a filter may claim at this version.
    public static ActionDiscriminator ActionsFor(Version negotiatedVersion)
    {
        if (negotiatedVersion == null)
            throw new ArgumentNullException(nameof(negotiatedVersion));

        return negotiatedVersion >= Highest ? VERSION_6_ACTIONS : VERSION_2_ACTIONS;
    }
}
