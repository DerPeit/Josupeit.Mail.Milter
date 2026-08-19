// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Session;

// What the negotiation actually settled on, as opposed to what the filter asked for.
//
// The two are not the same and the difference is the whole reason this type exists. A filter declares
// what it wants against no particular MTA; the answer is min(that, what this version defines, what the
// MTA granted). Every runtime decision that depends on a negotiated flag has to read it here rather
// than off ProtocolStateMachineConfiguration, or a filter's declaration would be honoured against an
// MTA that never agreed to it - which for the no-reply family means a suppressed answer the MTA is
// still waiting for.
//
// Paired with the configuration's IsAwaiting* by name: IsAwaitingX asks whether the filter wants the
// event, IsAnsweringX whether the MTA is waiting for a reply to it.
internal sealed record ProtocolNegotiationOutcome(
    Version ProtocolVersion,
    ActionDiscriminator RequiredActions,
    ContentBypassDiscriminator ProtocolContentOptions)
{
    // Nothing negotiated yet. Every event is answered and no version 6 behaviour is on, which is what
    // the library did before any of this was negotiable - so a session that somehow reads this before
    // its OPTNEG reply behaves like a version 2 one rather than misbehaving.
    public static readonly ProtocolNegotiationOutcome NotNegotiated = new(
        new Version(2, 0),
        ActionDiscriminator.None,
        ContentBypassDiscriminator.None);

    public bool IsAnsweringConnection => IsAnswering(ContentBypassDiscriminator.NoReplyToConnect);

    public bool IsAnsweringClientHello => IsAnswering(ContentBypassDiscriminator.NoReplyToClientHello);

    public bool IsAnsweringSender => IsAnswering(ContentBypassDiscriminator.NoReplyToSender);

    public bool IsAnsweringRecipients => IsAnswering(ContentBypassDiscriminator.NoReplyToRecipient);

    public bool IsAnsweringMessageData => IsAnswering(ContentBypassDiscriminator.NoReplyToMessageData);

    public bool IsAnsweringUnknownCommands => IsAnswering(ContentBypassDiscriminator.NoReplyToUnknownCommand);

    public bool IsAnsweringHeaders => IsAnswering(ContentBypassDiscriminator.NoReplyToHeader);

    public bool IsAnsweringEndOfHeaders => IsAnswering(ContentBypassDiscriminator.NoReplyToEndOfHeaders);

    public bool IsAnsweringBody => IsAnswering(ContentBypassDiscriminator.NoReplyToBody);

    public bool IsSkipGranted => ProtocolContentOptions.HasFlag(ContentBypassDiscriminator.SkipRemainingEvents);

    public bool KeepsLeadingHeaderSpace => ProtocolContentOptions.HasFlag(ContentBypassDiscriminator.LeadingHeaderSpace);

    private bool IsAnswering(ContentBypassDiscriminator noReplyFlag)
    {
        return !ProtocolContentOptions.HasFlag(noReplyFlag);
    }
}
