using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record ReplaceBodyAction : ModificationAction
{
    private const int CHUNK_SIZE = 65535;

    internal ReplaceBodyAction(Stream body, bool leaveOpen)
    {
        Body = body;
        LeaveOpen = leaveOpen;
    }

    public Stream Body { get; }

    public bool LeaveOpen { get; }

    protected override async IAsyncEnumerable<ResponsePacket> CreateResponsePackets([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                ReplaceBodyResponsePacket response;

                var memoryOwner = MemoryPool<byte>.Shared.Rent(CHUNK_SIZE);

                try
                {
                    // The pool is free to hand back more than was asked for, and reading into all of it
                    // would produce a chunk the protocol cannot frame.
                    var buffer = memoryOwner.Memory[..CHUNK_SIZE];
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
        finally
        {
            if (!LeaveOpen)
                await Body.DisposeAsync().ConfigureAwait(false);
        }
    }
}