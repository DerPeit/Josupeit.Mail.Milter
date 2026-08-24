// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using Josupeit.Practices.Mixins.Disposable;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// A slice of the message body (<c>SMFIC_BODY</c>).
/// </summary>
/// <remarks>
/// The body is sent in pieces - Postfix caps each at 64 KiB - so a chunk is not a meaningful unit of anything
/// except transport. The payload is pooled memory, which is why this packet is disposable and why its data must
/// be copied to outlive the handling of it.
/// </remarks>
[DisposableMixin(DisposableMixinOptions.WithoutAsyncDisposable)]
public sealed partial class BodyChunkRequestPacket : RequestPacket
{
    private readonly IMemoryOwner<byte>? _memory;
    private readonly Range _dataRange;

    /// <summary>
    /// Creates the packet around the memory the chunk arrived in.
    /// </summary>
    /// <param name="memory">
    /// The buffer holding the chunk, or <see langword="null"/> for an empty one. The packet takes
    /// ownership of it and returns it to the pool when disposed, so the caller must not keep it.
    /// </param>
    /// <param name="dataRange">The part of the buffer the chunk occupies.</param>
    public BodyChunkRequestPacket(IMemoryOwner<byte>? memory, Range dataRange)
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

    /// <inheritdoc />
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        ThrowIfDisposed();

        return visitor.VisitBodyChunkRequestPacket(this);
    }

    /// <summary>
    /// Returns the payload memory to the pool.
    /// </summary>
    private void DisposeManagedResources()
    {
        _memory?.Dispose();
    }
}
