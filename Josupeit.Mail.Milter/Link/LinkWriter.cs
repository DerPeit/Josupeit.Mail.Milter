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

    public virtual ValueTask WriteUInt32(uint value, CancellationToken cancellationToken)
    {
        var owner = MemoryPool<byte>.Shared.Rent(minBufferSize: 4);

        try
        {
            var slice = owner.Memory.Span[..4];

            BinaryPrimitives.WriteUInt32BigEndian(slice, value);

            return AwaitAndDispose(WriteData(slice, cancellationToken), owner);
        }
        catch
        {
            owner.Dispose();

            throw;
        }
    }

    public ValueTask WriteResponseCode(ResponseCode responseCode, CancellationToken cancellationToken)
    {
        var owner = MemoryPool<byte>.Shared.Rent(minBufferSize: 1);

        try
        {
            var slice = owner.Memory.Span[..1];

            slice[0] = (byte)responseCode;

            return AwaitAndDispose(WriteData(slice, cancellationToken), owner);
        }
        catch
        {
            owner.Dispose();

            throw;
        }
    }

    public virtual ValueTask WriteData(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        _pipeWriter.Write(data);

        return FlushAndAssert(cancellationToken);
    }

    private static async ValueTask AwaitAndDispose(ValueTask promise, IDisposable disposable)
    {
        using (disposable)
            await promise.ConfigureAwait(false);
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