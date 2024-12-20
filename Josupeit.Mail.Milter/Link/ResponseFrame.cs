using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public readonly record struct ResponseFrame(ResponseCode ResponseCode, IMemoryOwner<byte>? Memory = null, Range DataRange = default) : IDisposable
{
    private readonly TaskCompletionSource _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Dispose()
    {
        Memory?.Dispose();
    }

    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
    {
        return _taskCompletionSource.Task.ConfigureAwait(continueOnCapturedContext);
    }

    public TaskAwaiter GetAwaiter()
    {
        return _taskCompletionSource.Task.GetAwaiter();
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