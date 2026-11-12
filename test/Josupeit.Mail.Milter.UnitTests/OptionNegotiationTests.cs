// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

// The negotiation answers min(what the MTA offers, what this library implements), and masks every flag
// to what that version defines. Both halves matter: answering above the offer is rejected outright by
// Postfix, and naming a flag the negotiated version does not define can make libmilter drop the
// connection.
[TestFixture]
public sealed class OptionNegotiationTests
{
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 4)]
    [TestCase(6, 6)]
    public void The_answered_version_matches_an_mta_this_library_can_meet(int offered, int answered)
    {
        var response = Negotiate(offered);

        Assert.That(response.MilterProtocolVersion, Is.EqualTo(new Version(answered, 0)));
    }

    [Test]
    public void An_mta_newer_than_this_library_is_answered_with_the_highest_version_implemented()
    {
        var response = Negotiate(offeredVersion: 7);

        Assert.That(response.MilterProtocolVersion, Is.EqualTo(new Version(6, 0)));
    }

    [Test]
    public void There_is_no_protocol_version_5_so_an_mta_offering_one_is_answered_with_4()
    {
        var response = Negotiate(offeredVersion: 5);

        Assert.That(response.MilterProtocolVersion, Is.EqualTo(new Version(4, 0)));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void An_mta_below_the_floor_fails_negotiation(int offered)
    {
        Assert.That(() => Negotiate(offered), Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public void The_version_3_and_4_events_are_masked_away_from_a_version_2_mta()
    {
        var response = Negotiate(offeredVersion: 2);

        Assert.Multiple(() =>
        {
            Assert.That(response.MilterUndesiredProtocolContent.HasFlag(ContentBypassDiscriminator.NoUnknownCommand), Is.False);
            Assert.That(response.MilterUndesiredProtocolContent.HasFlag(ContentBypassDiscriminator.NoMessageData), Is.False);
        });
    }

    [Test]
    public void The_unknown_command_skip_arrives_at_version_3_and_the_data_skip_only_at_version_4()
    {
        var atVersion3 = Negotiate(offeredVersion: 3);
        var atVersion4 = Negotiate(offeredVersion: 4);

        Assert.Multiple(() =>
        {
            Assert.That(atVersion3.MilterUndesiredProtocolContent.HasFlag(ContentBypassDiscriminator.NoUnknownCommand), Is.True);
            Assert.That(atVersion3.MilterUndesiredProtocolContent.HasFlag(ContentBypassDiscriminator.NoMessageData), Is.False);
            Assert.That(atVersion4.MilterUndesiredProtocolContent.HasFlag(ContentBypassDiscriminator.NoMessageData), Is.True);
        });
    }

    [Test]
    public void An_event_the_mta_will_not_skip_is_dropped_rather_than_failing_the_session()
    {
        var mtaWillNotSkipTheBody = ContentBypassDiscriminator.All & ~ContentBypassDiscriminator.NoBody;

        var response = Negotiate(offeredVersion: 6, mtaContentBypass: mtaWillNotSkipTheBody);

        Assert.Multiple(() =>
        {
            Assert.That(response.MilterUndesiredProtocolContent.HasFlag(ContentBypassDiscriminator.NoBody), Is.False);
            Assert.That(response.MilterUndesiredProtocolContent.HasFlag(ContentBypassDiscriminator.NoHeader), Is.True);
        });
    }

    [Test]
    public void A_modification_the_mta_will_not_grant_fails_the_session()
    {
        Assert.That(
            () => Negotiate(
                offeredVersion: 6,
                actionRequirements: ActionRequirements.AddHeaders,
                mtaActions: ActionDiscriminator.None),
            Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public void A_version_6_modification_is_masked_away_from_a_version_2_mta()
    {
        var response = Negotiate(offeredVersion: 2, actionRequirements: ActionRequirements.AddHeaders);

        Assert.Multiple(() =>
        {
            Assert.That(response.MilterRequiredActions.HasFlag(ActionDiscriminator.AddHeaders), Is.True);
            Assert.That(response.MilterRequiredActions.HasFlag(ActionDiscriminator.ChangeSender), Is.False);
        });
    }

    // The requirement is derived from what the filter actually uses, so this gate only fires for a filter
    // that would genuinely lose something - a skip masked into a Continue, header values silently
    // trimmed. Everything else still degrades quietly.
    [TestCase(2, ProtocolVersionRequirement.Version2)]
    [TestCase(3, ProtocolVersionRequirement.Version3)]
    [TestCase(4, ProtocolVersionRequirement.Version4)]
    [TestCase(6, ProtocolVersionRequirement.Version6)]
    public void An_mta_that_meets_the_requirement_exactly_is_accepted(int offeredVersion, ProtocolVersionRequirement requirement)
    {
        var response = Negotiate(offeredVersion, versionRequirement: requirement);

        Assert.That(response.MilterProtocolVersion.Major, Is.EqualTo(offeredVersion));
    }

    [TestCase(2, ProtocolVersionRequirement.Version3)]
    [TestCase(4, ProtocolVersionRequirement.Version6)]
    [TestCase(2, ProtocolVersionRequirement.Version6)]
    public void An_mta_below_what_the_filter_needs_fails_negotiation(int offeredVersion, ProtocolVersionRequirement requirement)
    {
        Assert.That(
            () => Negotiate(offeredVersion, versionRequirement: requirement),
            Throws.InstanceOf<NotSupportedException>());
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(6)]
    public void A_filter_needing_nothing_newer_never_fails_over_the_version(int offeredVersion)
    {
        Assert.That(() => Negotiate(offeredVersion, versionRequirement: ProtocolVersionRequirement.Version2), Throws.Nothing);
    }

    // The outcome is what every later runtime decision reads, so it has to record what was settled
    // rather than what was asked for. A filter's declaration honoured against an MTA that never agreed
    // to it is exactly the bug the type exists to prevent.
    [Test]
    public void The_outcome_records_what_was_settled_rather_than_what_was_wanted()
    {
        var response = Negotiate(offeredVersion: 2, deliveryRequirements: DeliveryRequirements.RejectedRecipients);

        var outcome = OutcomeOf(response);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ProtocolVersion, Is.EqualTo(new Version(2, 0)));
            Assert.That(
                outcome.ProtocolContentOptions.HasFlag(ContentBypassDiscriminator.RejectedRecipients),
                Is.False,
                "The filter asked for it; a version 2 MTA does not define it.");
        });
    }

    // The one declaration whose loss is refused rather than absorbed. Every other flag degrades into
    // behaviour the filter already copes with; without SMFIP_HDR_LEADSPC the filter reads and writes
    // different header bytes than it was written against, on every message, and nothing else says so.
    [Test]
    public void A_filter_needing_the_leading_header_space_refuses_an_mta_below_version_6()
    {
        Assert.That(
            () => Negotiate(offeredVersion: 2, deliveryRequirements: DeliveryRequirements.LeadingHeaderSpace),
            Throws.InstanceOf<NotSupportedException>());
    }

    // The residual case the derived version requirement cannot cover: the MTA speaks version 6 and still
    // withholds this one bit. Postfix does not, but the failure would be silently altered header bytes.
    [Test]
    public void A_filter_needing_the_leading_header_space_refuses_a_version_6_mta_that_withholds_it()
    {
        Assert.That(
            () => Negotiate(
                offeredVersion: 6,
                mtaContentBypass: (ContentBypassDiscriminator)uint.MaxValue & ~ContentBypassDiscriminator.LeadingHeaderSpace,
                deliveryRequirements: DeliveryRequirements.LeadingHeaderSpace),
            Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public void The_leading_header_space_is_kept_when_the_mta_grants_it()
    {
        var response = Negotiate(offeredVersion: 6, deliveryRequirements: DeliveryRequirements.LeadingHeaderSpace);

        Assert.That(OutcomeOf(response).KeepsLeadingHeaderSpace, Is.True);
    }

    [Test]
    public void Every_event_is_answered_until_a_no_reply_flag_is_negotiated()
    {
        var response = Negotiate(offeredVersion: 6, protocolRequirements: ProtocolRequirements.All);

        var outcome = OutcomeOf(response);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsAnsweringConnection, Is.True);
            Assert.That(outcome.IsAnsweringClientHello, Is.True);
            Assert.That(outcome.IsAnsweringSender, Is.True);
            Assert.That(outcome.IsAnsweringRecipients, Is.True);
            Assert.That(outcome.IsAnsweringHeaders, Is.True);
            Assert.That(outcome.IsAnsweringEndOfHeaders, Is.True);
            Assert.That(outcome.IsAnsweringBody, Is.True);
        });
    }

    // SMFIP_SKIP announces a capability rather than asking for fewer events, so it is asked for on every
    // connection and the answer is whatever the MTA and the version allow. Nothing set it at all until
    // this was found: IsSkipGranted was false on every real connection, so every skip a filter answered
    // was quietly turned back into Continue and the whole feature was inert.
    [TestCase(6, true)]
    [TestCase(4, false)]
    [TestCase(2, false)]
    public void Skip_is_granted_only_by_an_mta_speaking_version_6(int offeredVersion, bool expected)
    {
        var response = Negotiate(offeredVersion, mtaContentBypass: (ContentBypassDiscriminator)uint.MaxValue);

        Assert.That(OutcomeOf(response).IsSkipGranted, Is.EqualTo(expected));
    }

    [Test]
    public void Skip_is_not_granted_by_a_version_6_mta_that_withholds_it()
    {
        var response = Negotiate(
            offeredVersion: 6,
            mtaContentBypass: (ContentBypassDiscriminator)uint.MaxValue & ~ContentBypassDiscriminator.SkipRemainingEvents);

        Assert.That(OutcomeOf(response).IsSkipGranted, Is.False);
    }

    // Naming macros needs a right of its own, and only real negotiation decides whether it was granted -
    // building the response packet by hand, which is all the frame tests can do, never sees that step.
    [Test]
    public void A_macro_list_travels_when_the_mta_grants_the_right_to_name_them()
    {
        var response = Negotiate(offeredVersion: 6, macroRequirements: MacroRequirements.For(ProtocolStage.EndOfBody, "i"));

        Assert.Multiple(() =>
        {
            Assert.That(response.MilterRequiredActions.HasFlag(ActionDiscriminator.SetMacroList), Is.True);
            Assert.That(response.MilterRequestedMacros, Is.Not.Null);
        });
    }

    // Degrades rather than failing, unlike every other action right: the MTA carries on sending its own
    // configured macro set. That set may lack a macro that was asked for, which the filter finds out at
    // runtime - refusing the MTA would cost every filter that reads only what it sends anyway.
    [TestCase(2, TestName = "An_mta_below_the_version_that_defines_it")]
    public void A_macro_list_the_negotiation_cannot_place_is_dropped_without_failing(int offeredVersion)
    {
        var response = Negotiate(offeredVersion, macroRequirements: MacroRequirements.For(ProtocolStage.EndOfBody, "i"));

        Assert.Multiple(() =>
        {
            Assert.That(response.MilterRequiredActions.HasFlag(ActionDiscriminator.SetMacroList), Is.False);
            Assert.That(response.MilterRequestedMacros, Is.Null);
        });
    }

    [Test]
    public void A_macro_list_is_dropped_by_a_version_6_mta_that_will_not_grant_the_right()
    {
        var response = Negotiate(
            offeredVersion: 6,
            mtaActions: ActionDiscriminator.All,
            macroRequirements: MacroRequirements.For(ProtocolStage.EndOfBody, "i"));

        Assert.That(response.MilterRequestedMacros, Is.Null);
    }

    // The states are handed an outcome built from exactly the three values this reply carries, and the
    // reply is where a test can still read them: nothing publishes the outcome to the machine any more,
    // because what the negotiation settled is not an invariant of the machine the way the configuration
    // is.
    private static ProtocolNegotiationOutcome OutcomeOf(OptionNegotiationResponsePacket response)
    {
        return new ProtocolNegotiationOutcome(
            response.MilterProtocolVersion,
            response.MilterRequiredActions,
            response.MilterUndesiredProtocolContent);
    }

    private static OptionNegotiationResponsePacket Negotiate(
        int offeredVersion,
        ContentBypassDiscriminator? mtaContentBypass = null,
        ActionDiscriminator? mtaActions = null,
        ProtocolRequirements protocolRequirements = ProtocolRequirements.EndOfBody,
        ActionRequirements actionRequirements = ActionRequirements.None,
        DeliveryRequirements deliveryRequirements = DeliveryRequirements.None,
        ProtocolVersionRequirement versionRequirement = ProtocolVersionRequirement.Version2,
        MacroRequirements? macroRequirements = null)
    {
        var stateMachine = new FakeProtocolStateMachine
        {
            Configuration = new ProtocolStateMachineConfiguration(
                protocolRequirements,
                actionRequirements,
                deliveryRequirements,
                NoReplyRequirements.None,
                versionRequirement,
                macroRequirements),
        };

        var state = new AwaitingNegotiationState(stateMachine, stateMachine.Configuration);

        var request = new OptionNegotiationRequestPacket
        {
            MtaProtocolVersion = new Version(offeredVersion, 0),
            MtaSupportedActions = mtaActions ?? ActionDiscriminator.All | ActionDiscriminator.SetMacroList,
            MtaSupportedContentBypass = mtaContentBypass ?? (ContentBypassDiscriminator)uint.MaxValue,
        };

        var responses = state.ProcessOptionNegotiationAttempt(request, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return responses.ToArrayAsync().AsTask().GetAwaiter().GetResult().OfType<OptionNegotiationResponsePacket>().Single();
    }
}
