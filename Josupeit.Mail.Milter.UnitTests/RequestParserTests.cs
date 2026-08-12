using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
[CancelAfter(10_000)]
public sealed class RequestParserTests
{
    private const uint DEFAULT_MAX_FRAME_LENGTH = 1024 * 1024;

    [Test]
    public async Task Zero_length_frame_is_rejected_instead_of_underflowing()
    {
        // Arrange
        var (parser, writer, reader) = CreateParserHarness();

        await writer.WriteAsync(Frame(length: 0, (byte)CommandCode.BodyChunk));

        // Act
        parser.Start();

        // Assert
        Assert.That(
            async () => await Drain(reader),
            Throws.InstanceOf<InvalidOperationException>());

        await parser.Stop();
    }

    [Test]
    public async Task Frame_longer_than_the_maximum_is_rejected()
    {
        // Arrange
        var (parser, writer, reader) = CreateParserHarness(maxFrameLength: 1024);

        await writer.WriteAsync(Frame(length: 1025, (byte)CommandCode.BodyChunk));

        // Act
        parser.Start();

        // Assert
        Assert.That(
            async () => await Drain(reader),
            Throws.InstanceOf<InvalidOperationException>());

        await parser.Stop();
    }

    [Test]
    public async Task Frame_at_the_maximum_length_is_accepted()
    {
        // Arrange
        var (parser, writer, reader) = CreateParserHarness(maxFrameLength: 11);

        var payload = Encoding.Latin1.GetBytes("localhost\0");

        await writer.WriteAsync(Frame(length: 11, Prepend((byte)CommandCode.Helo, payload)));

        // Act
        parser.Start();

        var frame = await reader.ReadAsync();

        // Assert
        using (frame)
        {
            Assert.Multiple(() =>
            {
                Assert.That(frame.CommandCode, Is.EqualTo(CommandCode.Helo));
                Assert.That(Encoding.Latin1.GetString(frame.Data.Span), Is.EqualTo("localhost\0"));
            });
        }

        await parser.Stop();
    }

    private static (RequestParser Parser, PipeWriter Writer, ChannelReader<RequestFrame> Reader) CreateParserHarness(
        uint maxFrameLength = DEFAULT_MAX_FRAME_LENGTH)
    {
        var pipe = new Pipe();

        var channel = Channel.CreateBounded<RequestFrame>(
            new BoundedChannelOptions(capacity: 1) { SingleReader = true, SingleWriter = true });

        var parser = new RequestParser(
            new LinkReader(pipe.Reader, maxFrameLength),
            channel.Writer,
            maxFrameLength);

        return (parser, pipe.Writer, channel.Reader);
    }

    private static byte[] Frame(uint length, params byte[] payload)
    {
        var bytes = new byte[4 + payload.Length];

        BinaryPrimitives.WriteUInt32BigEndian(bytes, length);
        payload.CopyTo(bytes, index: 4);

        return bytes;
    }

    private static byte[] Prepend(byte first, byte[] rest)
    {
        var bytes = new byte[1 + rest.Length];

        bytes[0] = first;
        rest.CopyTo(bytes, index: 1);

        return bytes;
    }

    private static async Task<List<RequestFrame>> Drain(ChannelReader<RequestFrame> reader)
    {
        var frames = new List<RequestFrame>();

        await foreach (var frame in reader.ReadAllAsync())
            frames.Add(frame);

        return frames;
    }
}
