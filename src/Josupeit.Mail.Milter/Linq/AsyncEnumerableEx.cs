// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

#pragma warning disable IDE0130 // namespace intentional
#pragma warning disable SA1201 // StyleCop does not understand C# 14 extension blocks
#pragma warning disable SA1402 // one-by-file not working for file-scoped types

namespace System.Linq;

[Embedded]
internal static class AsyncEnumerableEx
{
    public static IAsyncEnumerable<T> Return<T>(T value) => new ReturnAsyncEnumerable<T>(value);

    [SuppressMessage("ReSharper", "AsyncMethodWithoutAwait", Justification = "An async iterator that bridges synchronous IEnumerable to IAsyncEnumerable. The async keyword enables yield return; no await is needed because the source is synchronous.")]
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> Finally<T>(IAsyncEnumerable<T> source, Func<Task> action, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                yield return item;
        }
        finally
        {
            await action().ConfigureAwait(false);
        }
    }

    extension(AsyncEnumerable)
    {
        public static IAsyncEnumerable<T> Empty<T>() => EmptyAsyncEnumerable<T>.Instance;
    }

    extension<T>(IEnumerable<T> source)
    {
        public IAsyncEnumerable<T> ToAsyncEnumerable() => ToAsyncEnumerable(source, CancellationToken.None);

    }

    extension<T>(IAsyncEnumerable<T> source)
    {
        public IAsyncEnumerable<T> Finally(Func<Task> action) => Finally(source, action, CancellationToken.None);
    }
}

file sealed class EmptyAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    public static readonly EmptyAsyncEnumerable<T> Instance = new();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return EmptyAsyncEnumerator<T>.Instance;
    }
}

file sealed class EmptyAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    public static readonly EmptyAsyncEnumerator<T> Instance = new();

    public T Current => throw new InvalidOperationException();

    public ValueTask DisposeAsync() => default;

    public ValueTask<bool> MoveNextAsync() => new(false);
}

file sealed class ReturnAsyncEnumerable<T>(T value) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken) => new ReturnAsyncEnumerator<T>(value);
}

file sealed class ReturnAsyncEnumerator<T>(T value) : IAsyncEnumerator<T>
{
    private bool _hasNext = true;

    public T Current { get; private set; } = value;

    public ValueTask DisposeAsync()
    {
        Current = default!;

        return default;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        var hasNext = _hasNext;

        _hasNext = false;

        return new(hasNext);
    }
}
