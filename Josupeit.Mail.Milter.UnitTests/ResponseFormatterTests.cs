using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Channels;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ResponseFormatterTests
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    [Test]
    public async Task Frames_are_written_to_the_link()
    {
        // Arrange
        var (formatter, writer, pipe) = CreateFormatterHarness();

        var frame = new ResponseFrame(ResponseCode.ContinueProcessing);

        formatter.Start();

        // Act
        await writer.WriteAsync(frame);

        // Awaiting the frame is how a session waits for it to reach the wire. Stopping without this
        // would race the formatter's own start and cancel it before it consumed anything.
        await frame;

        writer.Complete();

        await StopAndAssertItCompletes(formatter);

        // Assert
        var result = await pipe.Reader.ReadAsync();

        Assert.That(
            result.Buffer.ToArray(),
            Is.EqualTo(new byte[] { 0, 0, 0, 1, (byte)ResponseCode.ContinueProcessing }));
    }

    [Test]
    public async Task Stopping_completes_once_the_channel_is_completed()
    {
        // Arrange
        var (formatter, writer, _) = CreateFormatterHarness();

        formatter.Start();

        // Act
        writer.Complete();

        // Assert
        await StopAndAssertItCompletes(formatter);
    }

    [Test]
    public async Task Starting_twice_is_rejected()
    {
        // Arrange
        var (formatter, writer, _) = CreateFormatterHarness();

        formatter.Start();

        // Act
        // Assert
        Assert.That(formatter.Start, Throws.InstanceOf<InvalidOperationException>());

        writer.Complete();

        await StopAndAssertItCompletes(formatter);
    }

    [Test]
    public void Stopping_before_starting_is_rejected()
    {
        // Arrange
        var (formatter, _, _) = CreateFormatterHarness();

        // Act
        // Assert
        Assert.That(async () => await formatter.StopAsync(), Throws.InstanceOf<InvalidOperationException>());
    }

    // Stopping a formatter that is waiting on a channel nobody completed never returns, which would hang
    // the whole test run rather than fail one test. So we bound the wait and assert on it instead.
    private static async Task StopAndAssertItCompletes(ResponseFormatter formatter)
    {
        var stopping = formatter.StopAsync();

        Assert.That(
            await Task.WhenAny(stopping, Task.Delay(StopTimeout)),
            Is.SameAs(stopping),
            "StopAsync did not complete.");

        await stopping;
    }

    private static (ResponseFormatter Formatter, ChannelWriter<ResponseFrame> Writer, Pipe Pipe) CreateFormatterHarness()
    {
        var pipe = new Pipe();

        var channel = Channel.CreateBounded<ResponseFrame>(
            new BoundedChannelOptions(capacity: 1) { SingleReader = true });

        var formatter = new ResponseFormatter(channel.Reader, new LinkWriter(pipe.Writer));

        return (formatter, channel.Writer, pipe);
    }
}
