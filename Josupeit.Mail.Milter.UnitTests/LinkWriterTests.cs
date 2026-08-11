using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
[CancelAfter(10_000)]
public sealed class LinkWriterTests
{
    [Test]
    public async Task Unsigned_integers_are_written_big_endian()
    {
        // Arrange
        var pipe = new Pipe();
        var linkWriter = new LinkWriter(pipe.Writer);

        // Act
        await linkWriter.WriteUInt32(0x01020304, CancellationToken.None);

        // Assert
        var result = await pipe.Reader.ReadAsync();

        Assert.That(result.Buffer.ToArray(), Is.EqualTo(new byte[] { 0x01, 0x02, 0x03, 0x04 }));
    }

    [Test]
    public async Task Response_codes_are_written_as_a_single_byte()
    {
        // Arrange
        var pipe = new Pipe();
        var linkWriter = new LinkWriter(pipe.Writer);

        // Act
        await linkWriter.WriteResponseCode(ResponseCode.ContinueProcessing, CancellationToken.None);

        // Assert
        var result = await pipe.Reader.ReadAsync();

        Assert.That(result.Buffer.ToArray(), Is.EqualTo(new[] { (byte)ResponseCode.ContinueProcessing }));
    }
}
