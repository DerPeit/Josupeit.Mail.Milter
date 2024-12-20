using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record ReplaceBodyAction(Stream Body) : ModificationAction
{
    private const int CHUNK_SIZE = 65535;

    protected override async IAsyncEnumerable<ResponsePacket> CreateResponsePackets([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using (Body.ConfigureAwait(false))
        {
            while (true)
            {
                ReplaceBodyResponsePacket response;

                var memoryOwner = MemoryPool<byte>.Shared.Rent(CHUNK_SIZE);

                try
                {
                    var buffer = memoryOwner.Memory;
                    var bytesRead = await Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        memoryOwner.Dispose();

                        yield break;
                    }

                    response = new ReplaceBodyResponsePacket
                    {
                        Memory = memoryOwner,
                        BodyRange = ..bytesRead,
                    };
                }
                catch
                {
                    memoryOwner.Dispose();

                    throw;
                }

                yield return response;
            }
        }
    }
}