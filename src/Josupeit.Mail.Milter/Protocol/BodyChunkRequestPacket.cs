// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// A slice of the message body (<c>SMFIC_BODY</c>).
/// </summary>
/// <remarks>
/// The body is sent in pieces - Postfix caps each at 64 KiB - so a chunk is not a meaningful unit of anything
/// except transport. The payload is pooled memory, which is why this packet is disposable and why its data must
/// be copied to outlive the handling of it.
/// </remarks>
public sealed class BodyChunkRequestPacket : RequestPacket, IDisposable
{
    private readonly IMemoryOwner<byte>? _memory;
    private readonly Range _dataRange;

    private bool _isDisposed;

    internal BodyChunkRequestPacket(IMemoryOwner<byte>? memory, Range dataRange)
    {
        _memory = memory;
        _dataRange = dataRange;
    }

    /// <summary>
    /// The bytes of this chunk. Valid only until the packet is disposed.
    /// </summary>
    public ReadOnlyMemory<byte> Data
    {
        get
        {
            ThrowIfDisposed();

            return _memory?.Memory[_dataRange] ?? ReadOnlyMemory<byte>.Empty;
        }
    }

    /// <summary>
    /// The bytes of this chunk as a span. Valid only until the packet is disposed.
    /// </summary>
    public ReadOnlySpan<byte> DataSpan => Data.Span;

    /// <summary>
    /// Returns the payload memory to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _memory?.Dispose();

        _isDisposed = true;
    }

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        ThrowIfDisposed();

        return visitor.VisitBodyChunkRequestPacket(this);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(GetType().ToString());
    }
}
