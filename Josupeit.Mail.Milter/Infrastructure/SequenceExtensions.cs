using System;
using System.Buffers;

namespace Josupeit.Mail.Milter.Infrastructure;

internal static class SequenceExtensions
{
    public delegate TOut FuncOfSpan<TIn, out TOut>(ReadOnlySpan<TIn> span);

    public static TOut AsSpanInvoke<TIn, TOut>(this ReadOnlySequence<TIn> sequence, FuncOfSpan<TIn, TOut> funcOfSpan)
    {
        if (funcOfSpan == null)
            throw new ArgumentNullException(nameof(funcOfSpan));

        if (sequence.IsSingleSegment)
            return funcOfSpan(sequence.FirstSpan);

        var sequenceLength = checked((int)sequence.Length);

        using var memoryOwner = MemoryPool<TIn>.Shared.Rent(sequenceLength);

        var memory = memoryOwner.Memory;
        var span = memory.Span;

        sequence.CopyTo(span);

        ReadOnlySpan<TIn> readOnlySpan
            = span.Length > sequenceLength
                ? span[..sequenceLength]
                : span;

        return funcOfSpan(readOnlySpan);
    }
}