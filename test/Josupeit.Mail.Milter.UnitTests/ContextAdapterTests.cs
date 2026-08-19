// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ContextAdapterTests
{
    [Test]
    public async Task Modifications_before_a_commit_action_are_passed_on()
    {
        Action[] actions = [ModificationActions.AddHeader("X-Test", "1"), CommitActions.Accept];

        var responses = await EnumerateEndOfBodyAsync(actions);

        Assert.That(responses, Has.Exactly(2).Items);
        Assert.That(responses[0], Is.InstanceOf<AddHeaderResponsePacket>());
        Assert.That(responses[1], Is.InstanceOf<AcceptMessageResponsePacket>());
    }

    [Test]
    public async Task Progress_may_be_reported_before_the_commit_action()
    {
        Action[] actions = [ProgressAction.Instance, ProgressAction.Instance, CommitActions.Continue];

        var responses = await EnumerateEndOfBodyAsync(actions);

        Assert.That(responses, Has.Exactly(3).Items);
        Assert.That(responses[2], Is.InstanceOf<ContinueProcessingResponsePacket>());
    }

    [Test]
    public void An_action_after_the_commit_action_is_rejected()
    {
        Action[] actions = [CommitActions.Accept, ModificationActions.AddHeader("X-Test", "1")];

        Assert.That(
            async () => await EnumerateEndOfBodyAsync(actions),
            Throws.InvalidOperationException);
    }

    [Test]
    public void A_second_commit_action_is_rejected()
    {
        Action[] actions = [CommitActions.Accept, CommitActions.Reject];

        Assert.That(
            async () => await EnumerateEndOfBodyAsync(actions),
            Throws.InvalidOperationException);
    }

    [Test]
    public void A_missing_commit_action_is_rejected()
    {
        Action[] actions = [ModificationActions.AddHeader("X-Test", "1")];

        Assert.That(
            async () => await EnumerateEndOfBodyAsync(actions),
            Throws.InvalidOperationException);
    }

    [Test]
    public void An_empty_sequence_is_rejected()
    {
        Assert.That(
            async () => await EnumerateEndOfBodyAsync([]),
            Throws.InvalidOperationException);
    }

    // The modifications reach the wire before the violation is noticed, which is unavoidable when the
    // filter streams them, so the packets seen up to that point are handed back with the throw.
    private static async Task<IReadOnlyList<ResponsePacket>> EnumerateEndOfBodyAsync(Action[] actions)
    {
        var adapter = new ContextAdapter(() => new EndOfBodyContext(actions), ProtocolNegotiationOutcome.NotNegotiated);
        var responses = new List<ResponsePacket>();

        await foreach (var response in adapter.OnEndOfBody(EndOfBodyRequestPacket.Instance))
        {
            responses.Add(response);

            (response as ReplaceBodyResponsePacket)?.Memory.Dispose();
        }

        return responses;
    }

    private sealed class EndOfBodyContext(IEnumerable<Action> actions) : IMilterContext
    {
        public static ProtocolRequirements ProtocolRequirements => ProtocolRequirements.All;

        public static ActionRequirements ActionRequirements => ActionRequirements.All;

        public IAsyncEnumerable<Action> OnEndOfBody()
        {
            return actions.ToAsyncEnumerable();
        }
    }
}
