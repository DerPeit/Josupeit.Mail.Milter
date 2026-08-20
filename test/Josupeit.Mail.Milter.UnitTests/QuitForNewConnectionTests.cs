// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

// SMFIC_QUIT_NC ends the SMTP connection and keeps the milter one, so a different client follows on the
// same socket. Two things have to be right, and neither is visible at the point it goes wrong: the state
// it lands in has to accept SMFIC_CONNECT, and the recorded connect packets have to be thrown away.
[TestFixture]
public sealed class QuitForNewConnectionTests
{
    [Test]
    public async Task Quitting_for_a_new_connection_expects_no_reply_and_drops_the_context()
    {
        var stateMachine = new FakeProtocolStateMachine();
        var state = new ConnectedState(stateMachine, TestNegotiation.Nothing);

        stateMachine.EnsureContextAdapter(TestNegotiation.Nothing);

        var responses = await Drain(state.ProcessQuittingForNewConnection(QuitForNewConnectionRequestPacket.Instance, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(responses, Is.Empty, "SMFIC_QUIT_NC expects no response.");
            Assert.That(stateMachine.ContextAdapterDisposalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_filter_wanting_the_connection_event_returns_to_recording_a_replay()
    {
        var stateMachine = CreateStateMachine(ProtocolRequirements.Connect);
        var state = new ConnectedState(stateMachine, TestNegotiation.Nothing);

        await Drain(state.ProcessQuittingForNewConnection(QuitForNewConnectionRequestPacket.Instance, CancellationToken.None));

        Assert.That(stateMachine.State, Is.InstanceOf<ReplayRecordingNegotiatedState>());
    }

    [Test]
    public async Task A_filter_not_wanting_the_connection_event_returns_to_the_negotiated_state()
    {
        var stateMachine = CreateStateMachine(ProtocolRequirements.EndOfBody);
        var state = new ConnectedState(stateMachine, TestNegotiation.Nothing);

        await Drain(state.ProcessQuittingForNewConnection(QuitForNewConnectionRequestPacket.Instance, CancellationToken.None));

        Assert.That(stateMachine.State, Is.InstanceOf<NegotiatedState>());
    }

    // The load-bearing one. An abort keeps the recorded connect packets, because it resets to before
    // HELO on the same connection and they are still true. A quit-for-new-connection must not, or the
    // next client's messages are reported against the previous client's address - silently, and for
    // every message that client sends.
    [Test]
    public async Task The_replay_after_a_new_connection_never_describes_the_previous_client()
    {
        var hostnames = new List<string>();
        var stateMachine = CreateStateMachine(ProtocolRequirements.Connect, () => new HostnameRecordingContext(hostnames));

        var recording = new ReplayRecordingNegotiatedState(stateMachine, TestNegotiation.Nothing);

        await Drain(recording.ProcessConnectionAttempt(Connect("gonefishing.example.com"), CancellationToken.None));
        await Drain(Current(stateMachine).ProcessQuittingForNewConnection(QuitForNewConnectionRequestPacket.Instance, CancellationToken.None));
        await Drain(Current(stateMachine).ProcessConnectionAttempt(Connect("newarrival.example.com"), CancellationToken.None));
        await Drain(Current(stateMachine).ProcessAbortion(AbortRequestPacket.Instance, CancellationToken.None));
        await Drain(Current(stateMachine).ProcessEndOfHeaders(EndOfHeadersRequestPacket.Instance, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(
                hostnames,
                Is.EqualTo(new[] { "gonefishing.example.com", "newarrival.example.com", "newarrival.example.com" }),
                "The replay must re-describe the client that is actually connected.");

            Assert.That(
                hostnames[2],
                Is.Not.EqualTo("gonefishing.example.com"),
                "Carrying the replay history across a quit reports the wrong peer for the whole connection.");
        });
    }

    [Test]
    public void Quitting_for_a_new_connection_may_not_precede_negotiation()
    {
        var stateMachine = new FakeProtocolStateMachine();
        var state = new AwaitingNegotiationState(stateMachine, stateMachine.Configuration);

        Assert.That(
            async () => await state.ProcessQuittingForNewConnection(QuitForNewConnectionRequestPacket.Instance, CancellationToken.None),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Quitting_for_a_new_connection_may_not_follow_a_quit()
    {
        var state = new DisconnectedState();

        Assert.That(
            async () => await state.ProcessQuittingForNewConnection(QuitForNewConnectionRequestPacket.Instance, CancellationToken.None),
            Throws.InstanceOf<InvalidOperationException>());
    }

    private static FakeProtocolStateMachine CreateStateMachine(ProtocolRequirements protocolRequirements, Func<IMilterCallbacks>? contextFactory = null)
    {
        return new FakeProtocolStateMachine(contextFactory)
        {
            Configuration = new ProtocolStateMachineConfiguration(protocolRequirements, ActionRequirements.None),
        };
    }

    private static ProtocolState Current(FakeProtocolStateMachine stateMachine)
    {
        return (ProtocolState)stateMachine.State!;
    }

    private static ConnectRequestPacket Connect(string hostName)
    {
        return new ConnectRequestPacket { HostName = hostName, EndPoint = null };
    }

    private static async Task<IReadOnlyList<ResponsePacket>> Drain(ValueTask<IAsyncEnumerable<ResponsePacket>> responses)
    {
        var drained = new List<ResponsePacket>();

        await foreach (var response in await responses)
            drained.Add(response);

        return drained;
    }

    private sealed class HostnameRecordingContext(List<string> hostnames) : IMilterCallbacks
    {
        public ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
        {
            hostnames.Add(info.Hostname);

            return new(CommitActions.Continue);
        }
    }
}
