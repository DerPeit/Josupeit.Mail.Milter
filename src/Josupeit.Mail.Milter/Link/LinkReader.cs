// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// Reads request frames off a <see cref="PipeReader"/>.
/// </summary>
/// <remarks>
/// The length prefix arrives from the peer and is used to size a read and an allocation, so it is
/// checked against the maximum this reader was given before either happens. A frame claiming more than
/// that is refused rather than trusted.
/// </remarks>
public class LinkReader : ILinkReader
{
    private readonly PipeReader _pipeReader;
    private readonly uint _maxDataLength;

    /// <summary>
    /// Creates a reader over a pipe.
    /// </summary>
    /// <param name="pipeReader">The pipe to read frames from.</param>
    /// <param name="maxDataLength">
    /// The largest payload to accept, in bytes. See <see cref="MilterOptions.MaxFrameLength"/>.
    /// </param>
    public LinkReader(PipeReader pipeReader, uint maxDataLength)
    {
        _pipeReader = pipeReader ?? throw new ArgumentNullException(nameof(pipeReader));
        _maxDataLength = maxDataLength;
    }

    /// <inheritdoc />
    public virtual async ValueTask<uint> ReadUInt32(CancellationToken cancellationToken)
    {
        var (memory, dataRange) = await ReadData(length: 4, cancellationToken).ConfigureAwait(false);

        using (memory)
        {
            var result = BinaryPrimitives.ReadUInt32BigEndian(memory!.Memory.Span[dataRange]);

            return result;
        }
    }

    /// <inheritdoc />
    public virtual async ValueTask<CommandCode> ReadCommandCode(CancellationToken cancellationToken)
    {
        var (memory, dataRange) = await ReadData(length: 1, cancellationToken).ConfigureAwait(false);

        using (memory)
        {
            var result = (CommandCode)memory!.Memory.Span[dataRange][0];

            return result;
        }
    }

    /// <inheritdoc />
    public virtual async ValueTask<(IMemoryOwner<byte>? Memory, Range DataRange)> ReadData(uint length, CancellationToken cancellationToken)
    {
        if (length == 0)
            return default;

        // Defence in depth. RequestParser bounds the frame length before it gets here, but this is the
        // method that buffers and then rents on an attacker-supplied size, so it enforces the bound too.
        if (length > _maxDataLength)
            throw new InvalidOperationException(
                $"Protocol violation: Requested {length} bytes, which exceeds the accepted maximum of {_maxDataLength} bytes.");

        ReadOnlySequence<byte> buffer;

        while (true)
        {
            var readResult = await _pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (readResult.IsCanceled)
                throw new InvalidOperationException(
                    $"Unable to read from {typeof(PipeReader)}. {typeof(PipeReader)}.{nameof(PipeReader.CancelPendingRead)} has been called.");

            buffer = readResult.Buffer;

            if (buffer.Length >= length)
                break;

            _pipeReader.AdvanceTo(buffer.Start, buffer.End);

            if (readResult.IsCompleted)
                throw new InvalidOperationException(
                    $"Unable to read from {typeof(PipeReader)}. Underlying stream reached its end.");
        }

        if (buffer.Length > length)
            buffer = buffer.Slice(start: 0, length);

        var memory = MemoryPool<byte>.Shared.Rent(minBufferSize: checked((int)length));

        try
        {
            buffer.CopyTo(memory.Memory.Span);

            _pipeReader.AdvanceTo(buffer.End);

            return (memory, new Range(new Index(0), new Index((int)length)));
        }
        catch
        {
            memory.Dispose();

            throw;
        }
    }
}
