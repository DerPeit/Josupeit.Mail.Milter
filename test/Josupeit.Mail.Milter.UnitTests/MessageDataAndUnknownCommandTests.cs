// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

// The two events an MTA only sends once it has been told a high enough protocol version. Nothing
// negotiates version 6 yet, so these cover the plumbing rather than the negotiation: that the states
// which should carry them do, that the states which should refuse them still do, and that asking for
// neither is what clears the two bypass flags.
[TestFixture]
public sealed class MessageDataAndUnknownCommandTests
{
    private static readonly UnknownCommandRequestPacket _unknownCommand = new() { CommandText = "WOTSIT" };

    [Test]
    public async Task The_data_verb_reaches_the_context_from_the_connected_state()
    {
        var stateMachine = new FakeProtocolStateMachine();
        var state = new ConnectedState(stateMachine);

        var responses = await Drain(state.ProcessMessageDataStart(MessageDataRequestPacket.Instance, CancellationToken.None));

        Assert.That(responses, Has.One.InstanceOf<ContinueProcessingResponsePacket>());
    }

    [Test]
    public async Task An_unrecognised_command_reaches_the_context_from_the_connected_state()
    {
        var stateMachine = new FakeProtocolStateMachine();
        var state = new ConnectedState(stateMachine);

        var responses = await Drain(state.ProcessUnknownCommand(_unknownCommand, CancellationToken.None));

        Assert.That(responses, Has.One.InstanceOf<ContinueProcessingResponsePacket>());
    }

    [Test]
    public void Neither_event_may_precede_negotiation()
    {
        var stateMachine = new FakeProtocolStateMachine();
        var state = new AwaitingNegotiationState(stateMachine, stateMachine.Configuration);

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await state.ProcessMessageDataStart(MessageDataRequestPacket.Instance, CancellationToken.None),
                Throws.InstanceOf<InvalidOperationException>());

            Assert.That(
                async () => await state.ProcessUnknownCommand(_unknownCommand, CancellationToken.None),
                Throws.InstanceOf<InvalidOperationException>());
        });
    }

    [Test]
    public void Neither_event_may_follow_a_quit()
    {
        var state = new DisconnectedState();

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await state.ProcessMessageDataStart(MessageDataRequestPacket.Instance, CancellationToken.None),
                Throws.InstanceOf<InvalidOperationException>());

            Assert.That(
                async () => await state.ProcessUnknownCommand(_unknownCommand, CancellationToken.None),
                Throws.InstanceOf<InvalidOperationException>());
        });
    }

    [Test]
    public void A_filter_wanting_neither_event_asks_the_mta_to_skip_both()
    {
        var configuration = new ProtocolStateMachineConfiguration(ProtocolRequirements.EndOfBody, ActionRequirements.None);

        var flags = configuration.ToContentBypassDiscriminator();

        Assert.Multiple(() =>
        {
            Assert.That(flags.HasFlag(ContentBypassDiscriminator.NoMessageData), Is.True);
            Assert.That(flags.HasFlag(ContentBypassDiscriminator.NoUnknownCommand), Is.True);
        });
    }

    [Test]
    public void A_filter_wanting_both_events_asks_the_mta_to_skip_neither()
    {
        var configuration = new ProtocolStateMachineConfiguration(
            ProtocolRequirements.MessageData | ProtocolRequirements.UnknownCommand,
            ActionRequirements.None);

        var flags = configuration.ToContentBypassDiscriminator();

        Assert.Multiple(() =>
        {
            Assert.That(flags.HasFlag(ContentBypassDiscriminator.NoMessageData), Is.False);
            Assert.That(flags.HasFlag(ContentBypassDiscriminator.NoUnknownCommand), Is.False);
        });
    }

    private static async Task<IReadOnlyList<ResponsePacket>> Drain(ValueTask<IAsyncEnumerable<ResponsePacket>> responses)
    {
        var drained = new List<ResponsePacket>();

        await foreach (var response in await responses)
            drained.Add(response);

        return drained;
    }
}
