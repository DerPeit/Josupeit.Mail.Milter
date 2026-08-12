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
    private const int MAXIMUM_CHUNK_LENGTH = 65535;

    [Test]
    public async Task Body_chunks_stay_within_the_protocol_maximum()
    {
        var body = new MemoryStream(new byte[3 * MAXIMUM_CHUNK_LENGTH]);
        var action = ModificationActions.ReplaceBody(body);
        var chunkLengths = new List<int>();

        await foreach (var packet in action.CreateResponsePackets())
        {
            var replaceBody = (ReplaceBodyResponsePacket)packet;

            chunkLengths.Add(replaceBody.Memory.Memory[replaceBody.BodyRange].Length);

            replaceBody.Memory.Dispose();
        }

        Assert.That(chunkLengths, Is.All.LessThanOrEqualTo(MAXIMUM_CHUNK_LENGTH));
        Assert.That(chunkLengths.Sum(), Is.EqualTo(3 * MAXIMUM_CHUNK_LENGTH));
    }

    [Test]
    public async Task The_body_stream_is_disposed_by_default()
    {
        var body = new MemoryStream([1, 2, 3]);
        var action = ModificationActions.ReplaceBody(body);

        await DrainAsync(action);

        Assert.That(body.CanRead, Is.False);
    }

    [Test]
    public async Task The_body_stream_is_left_open_on_request()
    {
        var body = new MemoryStream([1, 2, 3]);
        var action = ModificationActions.ReplaceBody(body, leaveOpen: true);

        await DrainAsync(action);

        Assert.That(body.CanRead, Is.True);
    }

    private static async Task DrainAsync(Action action)
    {
        await foreach (var packet in action.CreateResponsePackets())
            ((ReplaceBodyResponsePacket)packet).Memory.Dispose();
    }
}
