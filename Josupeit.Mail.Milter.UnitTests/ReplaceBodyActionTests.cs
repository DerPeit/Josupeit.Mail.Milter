using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ReplaceBodyActionTests
{
    private const int MaximumChunkLength = 65535;

    [Test]
    public async Task Body_chunks_stay_within_the_protocol_maximum()
    {
        // Arrange
        var body = new MemoryStream(new byte[3 * MaximumChunkLength]);
        var action = ModificationActions.ReplaceBody(body);
        var chunkLengths = new List<int>();

        // Act
        await foreach (var packet in action.CreateResponsePackets())
        {
            var replaceBody = (ReplaceBodyResponsePacket)packet;

            chunkLengths.Add(replaceBody.Memory.Memory[replaceBody.BodyRange].Length);

            replaceBody.Memory.Dispose();
        }

        // Assert
        Assert.That(chunkLengths, Is.All.LessThanOrEqualTo(MaximumChunkLength));
        Assert.That(chunkLengths.Sum(), Is.EqualTo(3 * MaximumChunkLength));
    }

    [Test]
    public async Task The_body_stream_is_disposed_by_default()
    {
        // Arrange
        var body = new MemoryStream([1, 2, 3]);
        var action = ModificationActions.ReplaceBody(body);

        // Act
        await DrainAsync(action);

        // Assert
        Assert.That(body.CanRead, Is.False);
    }

    [Test]
    public async Task The_body_stream_is_left_open_on_request()
    {
        // Arrange
        var body = new MemoryStream([1, 2, 3]);
        var action = ModificationActions.ReplaceBody(body, leaveOpen: true);

        // Act
        await DrainAsync(action);

        // Assert
        Assert.That(body.CanRead, Is.True);
    }

    private static async Task DrainAsync(Action action)
    {
        await foreach (var packet in action.CreateResponsePackets())
            ((ReplaceBodyResponsePacket)packet).Memory.Dispose();
    }
}
