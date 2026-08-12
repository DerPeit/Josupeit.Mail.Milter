// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Josupeit.Mail.Milter.Generator;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Replaces the body of the message with the contents of a stream.
/// </summary>
/// <remarks>
/// The stream is read once, in chunks, as the action is sent, so it must still be readable at that
/// point - which is after <see cref="IMilterCallbacks.OnEndOfBody"/> has yielded the action, not when it
/// was built. It replaces the body only; the headers are left exactly as they are, so a filter that
/// changes the content type or encoding has to say so with a header modification of its own. Requires
/// <see cref="ActionRequirements.ChangeBody"/>. Reached through
/// <see cref="ModificationActions.ReplaceBody"/>.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.ChangeBody))]
public sealed record ReplaceBodyAction : ModificationAction
{
    private const int CHUNK_SIZE = 65535;

    internal ReplaceBodyAction(Stream body, bool leaveOpen)
    {
        Body = body;
        LeaveOpen = leaveOpen;
    }

    /// <summary>
    /// The stream holding the replacement body.
    /// </summary>
    public Stream Body { get; }

    /// <summary>
    /// Whether <see cref="Body"/> is left open once it has been sent.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/>, the default, disposes the stream after the last chunk goes out. Pass
    /// <see langword="true"/> for a stream the caller owns and means to keep.
    /// </remarks>
    public bool LeaveOpen { get; }

    /// <inheritdoc />
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