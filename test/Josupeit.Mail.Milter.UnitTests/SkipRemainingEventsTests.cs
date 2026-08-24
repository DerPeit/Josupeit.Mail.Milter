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
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

// SMFIR_SKIP stops an event repeating without deciding anything about the message. The rules around it
// are deliberately asymmetric, and the asymmetry is the point: losing an optimisation must not lose
// mail, but asking to skip something that happens once is the filter saying something the protocol
// cannot express.
[TestFixture]
public sealed class SkipRemainingEventsTests
{
    [Test]
    public async Task Skipping_a_body_chunk_tells_the_mta_to_stop_sending_them()
    {
        var adapter = Adapter(isSkipGranted: true, () => new SkippingContext());

        var responses = await Drain(adapter.OnBodyChunk(BodyChunk()));

        Assert.That(responses, Has.One.InstanceOf<SkipRemainingEventsResponsePacket>());
    }

    [Test]
    public async Task Later_chunks_never_reach_the_callback_once_it_has_skipped()
    {
        var context = new SkippingContext();
        var adapter = Adapter(isSkipGranted: true, () => context);

        await Drain(adapter.OnBodyChunk(BodyChunk()));
        await Drain(adapter.OnBodyChunk(BodyChunk()));
        await Drain(adapter.OnBodyChunk(BodyChunk()));

        Assert.That(context.BodyChunks, Is.EqualTo(1), "The filter asked not to hear about these again.");
    }

    [Test]
    public async Task A_skipped_event_is_still_answered_so_the_stream_stays_in_step()
    {
        var adapter = Adapter(isSkipGranted: true, () => new SkippingContext());

        await Drain(adapter.OnBodyChunk(BodyChunk()));

        var responses = await Drain(adapter.OnBodyChunk(BodyChunk()));

        Assert.That(responses, Has.One.InstanceOf<ContinueProcessingResponsePacket>());
    }

    // The deliberate asymmetry. A skip the MTA declined costs traffic; throwing over it would cost the
    // message. Anyone tempted to "fix" this into a throw should read the other half of the pair below.
    [Test]
    public async Task A_skip_the_mta_did_not_grant_quietly_becomes_a_continue()
    {
        var context = new SkippingContext();
        var adapter = Adapter(isSkipGranted: false, () => context);

        var responses = await Drain(adapter.OnBodyChunk(BodyChunk()));

        Assert.Multiple(() =>
        {
            Assert.That(responses, Has.One.InstanceOf<ContinueProcessingResponsePacket>());
            Assert.That(responses, Has.None.InstanceOf<SkipRemainingEventsResponsePacket>());
        });
    }

    [Test]
    public async Task An_ungranted_skip_does_not_stop_the_events_arriving()
    {
        var context = new SkippingContext();
        var adapter = Adapter(isSkipGranted: false, () => context);

        await Drain(adapter.OnBodyChunk(BodyChunk()));
        await Drain(adapter.OnBodyChunk(BodyChunk()));

        Assert.That(context.BodyChunks, Is.EqualTo(2));
    }

    [Test]
    public void Skipping_an_event_that_happens_once_is_a_programming_error()
    {
        var adapter = Adapter(isSkipGranted: true, () => new SkippingContext());

        Assert.That(
            async () => await Drain(adapter.OnClientHello(new HeloRequestPacket { HeloString = "loud.example.com" })),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Skipping_cannot_be_the_verdict_on_a_message()
    {
        var adapter = Adapter(isSkipGranted: true, () => new SkippingAtEndOfBodyContext());

        Assert.That(
            async () => await Drain(adapter.OnEndOfBody(EndOfBodyRequestPacket.Instance)),
            Throws.InstanceOf<InvalidOperationException>());
    }

    // The adapter is disposed on ABORT and QUIT only, never after end-of-body, so one adapter can serve
    // two messages. Without the reset the second message would be starved of the events the first
    // skipped.
    [Test]
    public async Task A_skip_does_not_outlive_the_message_that_asked_for_it()
    {
        var context = new SkippingContext();
        var adapter = Adapter(isSkipGranted: true, () => context);

        await Drain(adapter.OnBodyChunk(BodyChunk()));
        await Drain(adapter.OnEndOfBody(EndOfBodyRequestPacket.Instance));
        await Drain(adapter.OnBodyChunk(BodyChunk()));

        Assert.That(context.BodyChunks, Is.EqualTo(2), "The next message starts listening again.");
    }

    private static ContextAdapter Adapter(bool isSkipGranted, Func<IMilterCallbacks> contextFactory)
    {
        var options = isSkipGranted ? ContentBypassDiscriminator.SkipRemainingEvents : ContentBypassDiscriminator.None;

        return new ContextAdapter(contextFactory, new ProtocolNegotiationOutcome(new Version(6, 0), ActionDiscriminator.None, options));
    }

    private static BodyChunkRequestPacket BodyChunk()
    {
        var owner = System.Buffers.MemoryPool<byte>.Shared.Rent(minBufferSize: 4);

        "body"u8.CopyTo(owner.Memory.Span);

        return new BodyChunkRequestPacket(owner, ..4);
    }

    private static async Task<IReadOnlyList<ResponsePacket>> Drain(IAsyncEnumerable<ResponsePacket> responses)
    {
        var drained = new List<ResponsePacket>();

        await foreach (var response in responses)
            drained.Add(response);

        return drained;
    }

    private sealed class SkippingContext : IMilterCallbacks
    {
        public int BodyChunks { get; private set; }

        public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
        {
            BodyChunks++;

            return new(CommitActions.SkipRemainingEvents);
        }

        public ValueTask<CommitAction> OnClientHello(ClientHelloInformation info, CancellationToken cancellationToken)
        {
            return new(CommitActions.SkipRemainingEvents);
        }
    }

    private sealed class SkippingAtEndOfBodyContext : IMilterCallbacks
    {
        public IAsyncEnumerable<Action> OnEndOfBody()
        {
            return new Action[] { CommitActions.SkipRemainingEvents }.ToAsyncEnumerable();
        }
    }
}
