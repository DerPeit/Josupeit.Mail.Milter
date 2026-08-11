using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ReplayingAbortedStateTests
{
    [Test]
    public async Task A_replayed_continue_produces_no_reply_to_a_macro()
    {
        // Arrange
        var stateMachine = new FakeProtocolStateMachine();
        var state = new ReplayingAbortedState(stateMachine, CreateReplayHistory());

        // Act
        var responses = await Drain(state.ProcessMacroDefinition(CreateMacroPacket(), CancellationToken.None));

        // Assert
        Assert.That(responses, Is.Empty, "SMFIC_MACRO expects no response.");
    }

    [Test]
    public void A_replayed_decision_is_refused_rather_than_written_in_reply_to_a_macro()
    {
        // Arrange
        var stateMachine = new FakeProtocolStateMachine(static () => new RejectingContext());
        var state = new ReplayingAbortedState(stateMachine, CreateReplayHistory());

        // Act
        // Assert
        Assert.That(
            async () => await Drain(state.ProcessMacroDefinition(CreateMacroPacket(), CancellationToken.None)),
            Throws.InstanceOf<InvalidOperationException>(),
            "Replying to a macro would desynchronise every later request and response.");
    }

    [Test]
    public async Task A_replayed_decision_is_written_in_reply_to_a_packet_that_expects_one()
    {
        // Arrange
        var stateMachine = new FakeProtocolStateMachine(static () => new RejectingContext());
        var state = new ReplayingAbortedState(stateMachine, CreateReplayHistory());

        var request = new HeloRequestPacket { HeloString = "mail.example.com" };

        // Act
        var responses = await Drain(state.ProcessClientHello(request, CancellationToken.None));

        // Assert
        Assert.That(responses, Has.Count.EqualTo(1));
    }

    private static IReadOnlyList<RequestPacket> CreateReplayHistory()
    {
        return
        [
            new ConnectRequestPacket { HostName = "mail.example.com", EndPoint = null },
        ];
    }

    private static DefineMacroRequestPacket CreateMacroPacket()
    {
        return new DefineMacroRequestPacket
        {
            CommandCode = CommandCode.Connect,
            Macros = new Dictionary<string, string> { ["j"] = "mail.example.com" },
        };
    }

    private static async Task<List<ResponsePacket>> Drain(IAsyncEnumerable<ResponsePacket> responses)
    {
        var drained = new List<ResponsePacket>();

        await foreach (var response in responses)
            drained.Add(response);

        return drained;
    }

    private sealed class RejectingContext : IMilterContext
    {
        public static ProtocolRequirements ProtocolRequirements { get; } = ProtocolRequirements.All;

        public static ActionRequirements ActionRequirements { get; } = ActionRequirements.None;

        public ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
        {
            return new(CommitActions.Reject);
        }
    }
}
