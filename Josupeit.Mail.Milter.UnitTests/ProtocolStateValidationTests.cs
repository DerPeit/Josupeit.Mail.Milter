using System;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

/// <summary>
/// The states return async iterators, whose bodies do not run until the sequence is enumerated. Argument
/// validation therefore has to sit outside the iterator, or it reports the fault from an unrelated stack
/// long after the offending call.
/// </summary>
[TestFixture]
public sealed class ProtocolStateValidationTests
{
    [Test]
    public void Connected_rejects_a_null_request_at_the_call()
    {
        // Arrange
        var state = new ConnectedState(new FakeProtocolStateMachine());

        // Act
        // Assert
        Assert.That(
            () => state.ProcessHeader(null!, CancellationToken.None),
            Throws.InstanceOf<ArgumentNullException>());
    }

    [Test]
    public void Negotiated_rejects_a_null_request_at_the_call()
    {
        // Arrange
        var state = new NegotiatedState(new FakeProtocolStateMachine());

        // Act
        // Assert
        Assert.That(
            () => state.ProcessMacroDefinition(null!, CancellationToken.None),
            Throws.InstanceOf<ArgumentNullException>());
    }

    [Test]
    public void Aborted_rejects_a_null_request_at_the_call()
    {
        // Arrange
        var state = new AbortedState(new FakeProtocolStateMachine());

        // Act
        // Assert
        Assert.That(
            () => state.ProcessQuitting(null!, CancellationToken.None),
            Throws.InstanceOf<ArgumentNullException>());
    }

    [Test]
    public void Awaiting_negotiation_rejects_an_unsupported_version_at_the_call()
    {
        // Arrange
        var stateMachine = new FakeProtocolStateMachine();

        var state = new AwaitingNegotiationState(stateMachine, stateMachine.Configuration);

        var request = new OptionNegotiationRequestPacket
        {
            MtaProtocolVersion = new Version(1, 0),
            MtaSupportedActions = default,
            MtaSupportedContentBypass = default,
        };

        // Act
        // Assert
        Assert.That(
            () => state.ProcessOptionNegotiationAttempt(request, CancellationToken.None),
            Throws.InstanceOf<NotSupportedException>());
    }
}
