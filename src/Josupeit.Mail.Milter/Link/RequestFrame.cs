// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// One frame as it arrived from the MTA: what it is, and the bytes that came with it.
/// </summary>
/// <remarks>
/// The payload is pooled memory rather than a copy, which is why this is disposable and why the frame
/// must not outlive the handling of it. <see cref="Protocol.RequestPacketFactory"/> turns the bytes into
/// a typed packet before the frame goes away.
/// </remarks>
/// <param name="CommandCode">Which command the frame carries.</param>
/// <param name="Memory">
/// The rented memory holding the payload, or <see langword="null"/> for a frame with no payload.
/// Disposing the frame returns it to the pool.
/// </param>
/// <param name="DataRange">The range within <paramref name="Memory"/> that the payload occupies.</param>
public readonly record struct RequestFrame(CommandCode CommandCode, IMemoryOwner<byte>? Memory, Range DataRange) : IDisposable
{
    internal ReadOnlyMemory<byte> Data => Memory?.Memory[DataRange] ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Returns the payload memory to the pool.
    /// </summary>
    public void Dispose()
    {
        Memory?.Dispose();
    }
}
