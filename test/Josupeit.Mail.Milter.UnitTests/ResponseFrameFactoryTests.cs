// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Linq;
using System.Text;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ResponseFrameFactoryTests
{
    [Test]
    public void Quarantining_carries_the_reason()
    {
        var packet = new QuarantineMessageResponsePacket { Reason = "suspicious" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.QuarantineMessage));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("suspicious\0")));
    }

    [Test]
    public void Replying_doubles_percent_signs_in_the_text()
    {
        var packet = new ReplyWithCodeResponsePacket { SmtpCode = "550", Text = "5.7.1 blocked 100% of the time" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.ReplyWithCode));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("550 5.7.1 blocked 100%% of the time\0")));
    }

    [Test]
    public void Replying_leaves_a_text_without_percent_signs_untouched()
    {
        var packet = new ReplyWithCodeResponsePacket { SmtpCode = "451", Text = "try again later" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(GetData(frame), Is.EqualTo(Latin1("451 try again later\0")));
    }

    [Test]
    public void Replying_doubles_adjacent_and_trailing_percent_signs()
    {
        var packet = new ReplyWithCodeResponsePacket { SmtpCode = "550", Text = "%%%" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(GetData(frame), Is.EqualTo(Latin1("550 %%%%%%\0")));
    }

    [Test]
    public void Changing_a_header_counts_occurrences_from_one_on_the_wire()
    {
        var packet = new ChangeOrDeleteHeaderResponsePacket { Index = 0, Name = "Received", Value = "redacted" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.ChangeOrDeleteHeader));
        Assert.That(GetData(frame), Is.EqualTo(new byte[] { 0, 0, 0, 1 }.Concat(Latin1("Received\0redacted\0"))));
    }

    [Test]
    public void Deleting_a_header_keeps_the_index_offset()
    {
        var packet = new ChangeOrDeleteHeaderResponsePacket { Index = 2, Name = "Received", Value = string.Empty };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(GetData(frame), Is.EqualTo(new byte[] { 0, 0, 0, 3 }.Concat(Latin1("Received\0\0"))));
    }

    [Test]
    public void Reporting_progress_carries_no_data()
    {
        using var frame = new ResponseFrameFactory().CreateResponseFrame(ProgressResponsePacket.Instance);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.Progress));
        Assert.That(GetData(frame), Is.Empty);
    }

    [Test]
    public void A_decision_without_data_is_a_bare_frame()
    {
        (ResponsePacket Packet, ResponseCode Code)[] decisions =
        [
            (AcceptMessageResponsePacket.Instance, ResponseCode.AcceptMessage),
            (ContinueProcessingResponsePacket.Instance, ResponseCode.ContinueProcessing),
            (DeferMessageResponsePacket.Instance, ResponseCode.DeferMessage),
            (DiscardMessageResponsePacket.Instance, ResponseCode.DiscardMessage),
            (RejectMessageResponsePacket.Instance, ResponseCode.RejectMessage),
        ];

        var factory = new ResponseFrameFactory();

        foreach (var (packet, code) in decisions)
        {
            using var frame = factory.CreateResponseFrame(packet);

            Assert.That(frame.ResponseCode, Is.EqualTo(code));
            Assert.That(GetData(frame), Is.Empty);
        }
    }

    [Test]
    public void Adding_a_header_terminates_both_fields()
    {
        var packet = new AddHeaderResponsePacket { Name = "X-Test", Value = "value" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.AddHeader));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("X-Test\0value\0")));
    }

    [Test]
    public void Adding_a_recipient_terminates_the_address()
    {
        var packet = new AddRecipientResponsePacket { Recipient = "bob@example.com" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.AddRecipient));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("bob@example.com\0")));
    }

    [Test]
    public void Deleting_a_recipient_terminates_the_address()
    {
        var packet = new DeleteRecipientResponsePacket { Recipient = "eve@example.com" };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.DeleteRecipient));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("eve@example.com\0")));
    }

    [Test]
    public void Negotiation_writes_three_big_endian_words()
    {
        var packet = new OptionNegotiationResponsePacket
        {
            MilterProtocolVersion = new Version(2, 0),
            MilterRequiredActions = ActionDiscriminator.AddHeaders,
            MilterUndesiredProtocolContent = ContentBypassDiscriminator.NoConnect | ContentBypassDiscriminator.NoHelo,
        };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.OptionNegotiation));
        Assert.That(
            GetData(frame),
            Is.EqualTo(new byte[]
            {
                0, 0, 0, 2,
                0, 0, 0, (byte)ActionDiscriminator.AddHeaders,
                0, 0, 0, (byte)(ContentBypassDiscriminator.NoConnect | ContentBypassDiscriminator.NoHelo),
            }));
    }

    [Test]
    public void Replacing_the_body_hands_the_buffer_through_untouched()
    {
        var memoryOwner = MemoryPool<byte>.Shared.Rent(16);

        "chunk"u8.CopyTo(memoryOwner.Memory.Span);

        var packet = new ReplaceBodyResponsePacket { Memory = memoryOwner, BodyRange = ..5 };

        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.ReplaceBody));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("chunk")));
    }

    [Test]
    public void An_unknown_packet_is_refused()
    {
        Assert.That(
            () => new ResponseFrameFactory().CreateResponseFrame(null!),
            Throws.ArgumentNullException);
    }

    private static byte[] Latin1(string text)
    {
        return Encoding.Latin1.GetBytes(text);
    }

    private static byte[] GetData(ResponseFrame frame)
    {
        return frame.Memory is null ? [] : frame.Memory.Memory[frame.DataRange].ToArray();
    }
}
