using System;
using System.Buffers;

namespace Josupeit.Mail.Milter.Protocol;

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

    public ReadOnlyMemory<byte> Data
    {
        get
        {
            ThrowIfDisposed();

            return _memory?.Memory[_dataRange] ?? ReadOnlyMemory<byte>.Empty;
        }
    }

    public ReadOnlySpan<byte> DataSpan => Data.Span;

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _memory?.Dispose();

        _isDisposed = true;
    }

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