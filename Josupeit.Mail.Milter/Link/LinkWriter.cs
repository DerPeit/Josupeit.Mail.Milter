using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public class LinkWriter : ILinkWriter
{
    private readonly PipeWriter _pipeWriter;

    public LinkWriter(PipeWriter pipeWriter)
    {
        _pipeWriter = pipeWriter ?? throw new ArgumentNullException(nameof(pipeWriter));
    }

    // WriteData hands the span straight to PipeWriter.Write, which copies it synchronously, so the
    // stack buffer is no longer referenced by the time the returned flush is awaited.
    public virtual ValueTask WriteUInt32(uint value, CancellationToken cancellationToken)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];

        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);

        return WriteData(buffer, cancellationToken);
    }

    public ValueTask WriteResponseCode(ResponseCode responseCode, CancellationToken cancellationToken)
    {
        Span<byte> buffer = stackalloc byte[sizeof(byte)];

        buffer[0] = (byte)responseCode;

        return WriteData(buffer, cancellationToken);
    }

    public virtual ValueTask WriteData(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        _pipeWriter.Write(data);

        return FlushAndAssert(cancellationToken);
    }

    private async ValueTask FlushAndAssert(CancellationToken cancellationToken)
    {
        var flushResult = await _pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (flushResult.IsCanceled)
            throw new InvalidOperationException(
                $"Unable to write to {typeof(PipeWriter)}. {typeof(PipeWriter)}.{nameof(PipeWriter.CancelPendingFlush)} has been called.");

        if (flushResult.IsCompleted)
            throw new InvalidOperationException(
                $"Unable to write to {typeof(PipeWriter)}. Underlying stream was closed, prematurely.");
    }
}