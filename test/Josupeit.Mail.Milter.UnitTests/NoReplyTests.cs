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

// The no-reply family is the one version 6 feature that fails quietly. One reply too many is read by the
// MTA as the answer to the next command, and every request and response after it is out of step for the
// rest of the connection - so the message that suffers is not the one that caused it. One reply too few
// leaves the MTA waiting until it times out.
//
// Both directions are pinned here: what goes on the wire, and what comes back out of the adapter.
[TestFixture]
public sealed class NoReplyTests
{
    private const ContentBypassDiscriminator ALL_NO_REPLY_FLAGS =
        ContentBypassDiscriminator.NoReplyToConnect
        | ContentBypassDiscriminator.NoReplyToClientHello
        | ContentBypassDiscriminator.NoReplyToSender
        | ContentBypassDiscriminator.NoReplyToRecipient
        | ContentBypassDiscriminator.NoReplyToMessageData
        | ContentBypassDiscriminator.NoReplyToUnknownCommand
        | ContentBypassDiscriminator.NoReplyToHeader
        | ContentBypassDiscriminator.NoReplyToEndOfHeaders
        | ContentBypassDiscriminator.NoReplyToBody;

    [TestCase(NoReplyRequirements.Connect, ContentBypassDiscriminator.NoReplyToConnect)]
    [TestCase(NoReplyRequirements.ClientHello, ContentBypassDiscriminator.NoReplyToClientHello)]
    [TestCase(NoReplyRequirements.Sender, ContentBypassDiscriminator.NoReplyToSender)]
    [TestCase(NoReplyRequirements.Recipient, ContentBypassDiscriminator.NoReplyToRecipient)]
    [TestCase(NoReplyRequirements.MessageData, ContentBypassDiscriminator.NoReplyToMessageData)]
    [TestCase(NoReplyRequirements.UnknownCommand, ContentBypassDiscriminator.NoReplyToUnknownCommand)]
    [TestCase(NoReplyRequirements.Header, ContentBypassDiscriminator.NoReplyToHeader)]
    [TestCase(NoReplyRequirements.EndOfHeaders, ContentBypassDiscriminator.NoReplyToEndOfHeaders)]
    [TestCase(NoReplyRequirements.Body, ContentBypassDiscriminator.NoReplyToBody)]
    public void Each_event_declared_no_reply_sets_exactly_its_own_flag(NoReplyRequirements declared, ContentBypassDiscriminator expected)
    {
        var response = Negotiate(offeredVersion: 6, declared);

        Assert.That(response.MilterUndesiredProtocolContent & ALL_NO_REPLY_FLAGS, Is.EqualTo(expected));
    }

    [Test]
    public void The_whole_family_is_asked_for_at_once_when_every_event_is_quiet()
    {
        var response = Negotiate(offeredVersion: 6, NoReplyRequirements.All);

        Assert.That(response.MilterUndesiredProtocolContent & ALL_NO_REPLY_FLAGS, Is.EqualTo(ALL_NO_REPLY_FLAGS));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void No_reply_is_masked_away_from_an_mta_below_version_6(int offeredVersion)
    {
        var response = Negotiate(offeredVersion, NoReplyRequirements.All);

        Assert.That(response.MilterUndesiredProtocolContent & ALL_NO_REPLY_FLAGS, Is.EqualTo(ContentBypassDiscriminator.None));
    }

    [Test]
    public void No_reply_is_dropped_when_a_version_6_mta_does_not_offer_it()
    {
        var response = Negotiate(offeredVersion: 6, NoReplyRequirements.All, mtaContentBypass: ContentBypassDiscriminator.All);

        Assert.That(response.MilterUndesiredProtocolContent & ALL_NO_REPLY_FLAGS, Is.EqualTo(ContentBypassDiscriminator.None));
    }

    // Every event, not one of them. The adapter picks one of nine IsAnswering properties by hand, one per
    // event, and a copy-and-paste slip there wires an event to its neighbour's flag - which is the
    // desynchronisation this whole file exists to catch, and which testing only OnHeader would miss.
    [TestCase(ContentBypassDiscriminator.NoReplyToConnect)]
    [TestCase(ContentBypassDiscriminator.NoReplyToClientHello)]
    [TestCase(ContentBypassDiscriminator.NoReplyToSender)]
    [TestCase(ContentBypassDiscriminator.NoReplyToRecipient)]
    [TestCase(ContentBypassDiscriminator.NoReplyToMessageData)]
    [TestCase(ContentBypassDiscriminator.NoReplyToUnknownCommand)]
    [TestCase(ContentBypassDiscriminator.NoReplyToHeader)]
    [TestCase(ContentBypassDiscriminator.NoReplyToEndOfHeaders)]
    [TestCase(ContentBypassDiscriminator.NoReplyToBody)]
    public async Task An_event_the_mta_is_not_reading_is_answered_with_nothing(ContentBypassDiscriminator flag)
    {
        var adapter = Adapter(flag, static () => new TestMilterContext());

        var responses = await Drain(Deliver(adapter, flag));

        Assert.That(responses, Is.Empty);
    }

    [TestCase(ContentBypassDiscriminator.NoReplyToConnect)]
    [TestCase(ContentBypassDiscriminator.NoReplyToClientHello)]
    [TestCase(ContentBypassDiscriminator.NoReplyToSender)]
    [TestCase(ContentBypassDiscriminator.NoReplyToRecipient)]
    [TestCase(ContentBypassDiscriminator.NoReplyToMessageData)]
    [TestCase(ContentBypassDiscriminator.NoReplyToUnknownCommand)]
    [TestCase(ContentBypassDiscriminator.NoReplyToHeader)]
    [TestCase(ContentBypassDiscriminator.NoReplyToEndOfHeaders)]
    [TestCase(ContentBypassDiscriminator.NoReplyToBody)]
    public async Task The_same_event_is_answered_normally_when_its_flag_was_not_negotiated(ContentBypassDiscriminator flag)
    {
        var adapter = Adapter(ContentBypassDiscriminator.None, static () => new TestMilterContext());

        var responses = await Drain(Deliver(adapter, flag));

        Assert.That(responses, Has.One.InstanceOf<ContinueProcessingResponsePacket>());
    }

    // One arm per event the MTA can be told not to wait for, which is what makes the two tests above
    // cover all nine rather than headers alone. End-of-body has no arm because the protocol gives it no
    // flag: its verdict is the reason the event exists.
    private static IAsyncEnumerable<ResponsePacket> Deliver(ContextAdapter adapter, ContentBypassDiscriminator flag)
    {
        return flag switch
        {
            ContentBypassDiscriminator.NoReplyToConnect => adapter.OnConnectionEstablished(Connect()),
            ContentBypassDiscriminator.NoReplyToClientHello => adapter.OnClientHello(new HeloRequestPacket { HeloString = "quietly.example.com" }),
            ContentBypassDiscriminator.NoReplyToSender => adapter.OnSender(new SenderRequestPacket { Sender = "<norbert.noreply@example.com>", ESmtpArguments = [] }),
            ContentBypassDiscriminator.NoReplyToRecipient => adapter.OnRecipient(new RecipientRequestPacket { Recipient = "<hushed.hazel@example.com>", ESmtpArguments = [] }),
            ContentBypassDiscriminator.NoReplyToMessageData => adapter.OnMessageDataStarted(MessageDataRequestPacket.Instance),
            ContentBypassDiscriminator.NoReplyToUnknownCommand => adapter.OnUnknownCommand(new UnknownCommandRequestPacket { CommandText = "SPLOSH" }),
            ContentBypassDiscriminator.NoReplyToHeader => adapter.OnHeader(Header()),
            ContentBypassDiscriminator.NoReplyToEndOfHeaders => adapter.OnEndOfHeaders(EndOfHeadersRequestPacket.Instance),
            ContentBypassDiscriminator.NoReplyToBody => adapter.OnBodyChunk(new BodyChunkRequestPacket(null, ..0)),
            _ => throw new ArgumentOutOfRangeException(nameof(flag), flag, "No event is paired with this flag."),
        };
    }

    // Should be unreachable once the flag is derived - the analysis sets it only after proving the
    // callback cannot answer anything else - which makes this a check on the analysis as much as on a
    // hand-written declaration.
    [Test]
    public void A_decision_from_an_event_the_mta_is_not_reading_ends_the_session()
    {
        var adapter = Adapter(ContentBypassDiscriminator.NoReplyToHeader, static () => new RejectingContext());

        Assert.That(
            async () => await Drain(adapter.OnHeader(Header())),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task End_of_body_is_answered_even_when_every_other_event_is_quiet()
    {
        var adapter = Adapter(ALL_NO_REPLY_FLAGS, static () => new TestMilterContext());

        var responses = await Drain(adapter.OnEndOfBody(EndOfBodyRequestPacket.Instance));

        Assert.That(responses, Has.One.InstanceOf<ContinueProcessingResponsePacket>());
    }

    // The transition off ReplayRecordingNegotiatedState hangs off the response sequence completing. With
    // the connect reply suppressed that sequence is empty, and if Finally did not run the machine would
    // sit there for the rest of the connection with its replay history growing per event.
    [Test]
    public async Task Recording_a_replay_still_moves_on_when_the_connect_reply_is_suppressed()
    {
        var stateMachine = new FakeProtocolStateMachine
        {
            Configuration = new ProtocolStateMachineConfiguration(ProtocolRequirements.Connect, ActionRequirements.None),
        };

        var state = new ReplayRecordingNegotiatedState(stateMachine, Outcome(ContentBypassDiscriminator.NoReplyToConnect));

        var responses = await Drain(state.ProcessConnectionAttempt(Connect(), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(responses, Is.Empty);
            Assert.That(stateMachine.State, Is.InstanceOf<ReplayingConnectedState>());
        });
    }

    // responseExpected was hardcoded true for every event here. Once any no-reply flag is granted that is
    // wrong, and wrong in the direction that desynchronises the connection.
    [Test]
    public void A_replayed_decision_is_refused_when_the_triggering_event_is_not_answered()
    {
        var stateMachine = new FakeProtocolStateMachine(static () => new RejectingContext());

        var state = new ReplayingAbortedState(stateMachine, Outcome(ContentBypassDiscriminator.NoReplyToHeader), [Connect()]);

        // Named, because more than one InvalidOperationException is reachable from this call and only one
        // of them is the point. This one comes from the replay path rather than from the context adapter:
        // the recorded connect callback answered a rejection while replaying under an event the MTA is
        // not reading a reply to, so there is nowhere to put it.
        Assert.That(
            async () => await Drain(state.ProcessHeader(Header(), CancellationToken.None)),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("expects no reply"));
    }

    private static ProtocolNegotiationOutcome Outcome(ContentBypassDiscriminator options)
    {
        return new ProtocolNegotiationOutcome(new Version(6, 0), ActionDiscriminator.None, options);
    }

    private static ContextAdapter Adapter(ContentBypassDiscriminator options, Func<IMilterCallbacks> contextFactory)
    {
        return new ContextAdapter(contextFactory, Outcome(options));
    }

    private static HeaderRequestPacket Header()
    {
        return new HeaderRequestPacket { Name = "Subject", Value = "Quiet please" };
    }

    private static ConnectRequestPacket Connect()
    {
        return new ConnectRequestPacket { HostName = "quietly.example.com", EndPoint = null };
    }

    private static OptionNegotiationResponsePacket Negotiate(
        int offeredVersion,
        NoReplyRequirements noReply,
        ContentBypassDiscriminator? mtaContentBypass = null)
    {
        var stateMachine = new FakeProtocolStateMachine
        {
            Configuration = new ProtocolStateMachineConfiguration(
                ProtocolRequirements.All,
                ActionRequirements.None,
                DeliveryRequirements.None,
                noReply),
        };

        var state = new AwaitingNegotiationState(stateMachine, stateMachine.Configuration);

        var request = new OptionNegotiationRequestPacket
        {
            MtaProtocolVersion = new Version(offeredVersion, 0),
            MtaSupportedActions = ActionDiscriminator.All,
            MtaSupportedContentBypass = mtaContentBypass ?? (ContentBypassDiscriminator)uint.MaxValue,
        };

        var responses = state.ProcessOptionNegotiationAttempt(request, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return responses.ToArrayAsync().AsTask().GetAwaiter().GetResult().OfType<OptionNegotiationResponsePacket>().Single();
    }

    private static async Task<IReadOnlyList<ResponsePacket>> Drain(IAsyncEnumerable<ResponsePacket> responses)
    {
        var drained = new List<ResponsePacket>();

        await foreach (var response in responses)
            drained.Add(response);

        return drained;
    }

    private static async Task<IReadOnlyList<ResponsePacket>> Drain(ValueTask<IAsyncEnumerable<ResponsePacket>> responses)
    {
        return await Drain(await responses);
    }

    private sealed class RejectingContext : IMilterCallbacks
    {
        public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
        {
            return new(CommitActions.Reject);
        }

        public ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
        {
            return new(CommitActions.Reject);
        }
    }
}
