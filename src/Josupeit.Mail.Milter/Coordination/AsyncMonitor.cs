// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Coordination;

// SemaphoreSlim is IDisposable, but only to release a ManualResetEvent allocated exclusively
// through AvailableWaitHandle. This class never reads that property, so nothing is allocated
// and there is nothing to dispose.
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "SemaphoreSlim allocates a disposable handle only through AvailableWaitHandle, which is never called here.")]
internal sealed class AsyncMonitor
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Queue<TaskCompletionSource> _waiters = new();

    public async ValueTask<Releaser> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        return new Releaser(this);
    }

    public void Pulse()
    {
        while (_waiters.TryDequeue(out var tcs))
        {
            if (tcs.TrySetResult())
                return;
        }
    }

    public void PulseAll()
    {
        while (_waiters.TryDequeue(out var tcs))
            tcs.TrySetResult();
    }

    [SuppressMessage("ReSharper", "UseAwaitUsing", Justification = "The callback is a single TrySetCanceled call; await using would box the CancellationTokenRegistration through the IAsyncDisposable extension.")]
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _waiters.Enqueue(tcs);
        _lock.Release();

        try
        {
            using (cancellationToken.Register(static s => ((TaskCompletionSource)s!).TrySetCanceled(), tcs))
                await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    // Returned by value from EnterAsync so the using-statement disposes it through a
    // constrained call rather than boxing it to IDisposable. A ref struct would avoid
    // the interface entirely, but ValueTask<T> cannot carry a ref struct as T.
    public readonly struct Releaser(AsyncMonitor monitor) : IDisposable
    {
        public void Dispose() => monitor._lock.Release();
    }
}
