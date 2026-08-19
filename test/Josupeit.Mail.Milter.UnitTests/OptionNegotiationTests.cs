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

    private static OptionNegotiationResponsePacket Negotiate(
        int offeredVersion,
        ContentBypassDiscriminator? mtaContentBypass = null,
        ActionDiscriminator? mtaActions = null,
        ProtocolRequirements protocolRequirements = ProtocolRequirements.EndOfBody,
        ActionRequirements actionRequirements = ActionRequirements.None)
    {
        var stateMachine = new FakeProtocolStateMachine
        {
            Configuration = new ProtocolStateMachineConfiguration(protocolRequirements, actionRequirements),
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
