// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// One frame on its way to the MTA, and a way to wait until it has actually gone.
/// </summary>
/// <remarks>
/// <para>
/// The frame is handed to the formatter through a channel and written from there, so returning from the
/// hand-off says nothing about whether the bytes left. Awaiting the frame itself does: it is awaitable in
/// its own right, and the await completes when the formatter has written it.
/// </para>
/// <para>
/// The payload is pooled memory rather than a copy, which is why this is disposable.
/// </para>
/// </remarks>
/// <param name="ResponseCode">Which response the frame carries.</param>
/// <param name="Memory">
/// The rented memory holding the payload, or <see langword="null"/> for a frame with no payload.
/// Disposing the frame returns it to the pool.
/// </param>
/// <param name="DataRange">The range within <paramref name="Memory"/> that the payload occupies.</param>
public readonly record struct ResponseFrame(ResponseCode ResponseCode, IMemoryOwner<byte>? Memory = null, Range DataRange = default) : IDisposable
{
    private readonly TaskCompletionSource _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Returns the payload memory to the pool.
    /// </summary>
    public void Dispose()
    {
        Memory?.Dispose();
    }

    /// <summary>
    /// Configures how the await on this frame resumes.
    /// </summary>
    /// <param name="continueOnCapturedContext">
    /// <see langword="true"/> to resume on the captured context; <see langword="false"/> otherwise.
    /// </param>
    /// <returns>An awaitable that completes once the frame has been written to the link.</returns>
    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
    {
        return _taskCompletionSource.Task.ConfigureAwait(continueOnCapturedContext);
    }

    /// <summary>
    /// Gets the awaiter, which is what makes the frame itself awaitable.
    /// </summary>
    /// <returns>An awaiter that completes once the frame has been written to the link.</returns>
    public TaskAwaiter GetAwaiter()
    {
        return _taskCompletionSource.Task.GetAwaiter();
    }

    // Waits for the frame to reach the link, giving up after the timeout.
    //
    // A peer that stops reading leaves the formatter unable to flush, which would otherwise park the
    // session here indefinitely and, because the session's cleanup never runs, hang Milter.Stop with it.
    internal Task WaitAsync(TimeSpan timeout, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        return _taskCompletionSource.Task.WaitAsync(timeout, timeProvider, cancellationToken);
    }

    internal void SetCompleted()
    {
        _taskCompletionSource.SetResult();
    }

    internal void SetCanceled(CancellationToken cancellationToken)
    {
        _taskCompletionSource.SetCanceled(cancellationToken);
    }

    internal void SetException(Exception ex)
    {
        _taskCompletionSource.SetException(ex);
    }
}
