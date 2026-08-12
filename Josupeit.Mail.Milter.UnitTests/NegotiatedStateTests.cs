using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class NegotiatedStateTests
{
    [Test]
    public async Task Aborting_drops_the_context_without_replying()
    {
        // Arrange
        var stateMachine = new FakeProtocolStateMachine();
        var state = new NegotiatedState(stateMachine);

        stateMachine.EnsureContextAdapter();

        // Act
        var responses = await Drain(state.ProcessAbortion(AbortRequestPacket.Instance, CancellationToken.None));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(responses, Is.Empty, "SMFIC_ABORT expects no response.");
            Assert.That(stateMachine.ContextAdapterDisposalCount, Is.EqualTo(1));
            Assert.That(stateMachine.State, Is.Null, "An abortion here is already at the reset point.");
        });
    }

    [Test]
    public async Task Aborting_while_recording_a_replay_drops_the_context_without_replying()
    {
        // Arrange
        var stateMachine = new FakeProtocolStateMachine();
        var state = new ReplayRecordingNegotiatedState(stateMachine);

        stateMachine.EnsureContextAdapter();

        // Act
        var responses = await Drain(state.ProcessAbortion(AbortRequestPacket.Instance, CancellationToken.None));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(responses, Is.Empty, "SMFIC_ABORT expects no response.");
            Assert.That(stateMachine.ContextAdapterDisposalCount, Is.EqualTo(1));
            Assert.That(stateMachine.State, Is.Null, "An abortion here is already at the reset point.");
        });
    }

    [Test]
    public void Aborting_before_negotiation_is_still_a_protocol_violation()
    {
        // Arrange
        var stateMachine = new FakeProtocolStateMachine();

        var state = new AwaitingNegotiationState(stateMachine, stateMachine.Configuration);

        // Act
        // Assert
        Assert.That(
            () => state.ProcessAbortion(AbortRequestPacket.Instance, CancellationToken.None),
            Throws.InstanceOf<InvalidOperationException>());
    }

    private static async Task<List<ResponsePacket>> Drain(ValueTask<IAsyncEnumerable<ResponsePacket>> responses)
    {
        var drained = new List<ResponsePacket>();

        await foreach (var response in await responses.ConfigureAwait(false))
            drained.Add(response);

        return drained;
    }
}
