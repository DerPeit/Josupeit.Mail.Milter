// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Coordination;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class AsyncMonitorTests
{
    private AsyncMonitor _monitor = null!;

    [SetUp]
    public void SetUp()
    {
        _monitor = new AsyncMonitor();
    }

    [Test]
    public async Task Enter_acquires_the_lock()
    {
        using var releaser = await _monitor.EnterAsync().ConfigureAwait(false);

        Assert.That(releaser, Is.Not.EqualTo(default(AsyncMonitor.Releaser)));
    }

    [Test]
    public async Task Enter_blocks_while_the_lock_is_held()
    {
        using var first = await _monitor.EnterAsync().ConfigureAwait(false);

        var second = _monitor.EnterAsync();

        Assert.That(second.IsCompleted, Is.False);
    }

    [Test]
    public async Task Dispose_releases_the_lock()
    {
        var first = await _monitor.EnterAsync().ConfigureAwait(false);

        first.Dispose();

        var second = _monitor.EnterAsync();

        Assert.That(second.IsCompleted, Is.True);
    }

    [Test]
    public async Task Wait_releases_the_lock_and_re_acquires_after_pulse()
    {
        var entered = new SemaphoreSlim(0);
        var done = new TaskCompletionSource();

        var waiter = Task.Run(async () =>
        {
            using (await _monitor.EnterAsync().ConfigureAwait(false))
            {
                entered.Release();

                await _monitor.WaitAsync().ConfigureAwait(false);

                done.SetResult();
            }
        });

        // Wait until the background task holds the lock and enters WaitAsync.
        await entered.WaitAsync().ConfigureAwait(false);

        // The waiter released the lock inside WaitAsync - we can enter now.
        using (await _monitor.EnterAsync().ConfigureAwait(false))
            _monitor.PulseAll();

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.That(done.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task PulseAll_wakes_every_waiter()
    {
        var waiterCount = 3;
        var allWaiting = new SemaphoreSlim(0);
        var waiters = new Task[waiterCount];

        for (var i = 0; i < waiterCount; i++)
        {
            waiters[i] = Task.Run(async () =>
            {
                using (await _monitor.EnterAsync().ConfigureAwait(false))
                {
                    allWaiting.Release();

                    await _monitor.WaitAsync().ConfigureAwait(false);
                }
            });
        }

        // Wait until all three are inside WaitAsync and have released the lock.
        for (var i = 0; i < waiterCount; i++)
            await allWaiting.WaitAsync().ConfigureAwait(false);

        using (await _monitor.EnterAsync().ConfigureAwait(false))
            _monitor.PulseAll();

        await Task.WhenAll(waiters).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.That(waiters, Has.All.Property(nameof(Task.IsCompletedSuccessfully)).True);
    }

    [Test]
    public async Task Pulse_wakes_exactly_one_waiter()
    {
        var allWaiting = new SemaphoreSlim(0);

        var first = Task.Run(async () =>
        {
            using (await _monitor.EnterAsync().ConfigureAwait(false))
            {
                allWaiting.Release();

                await _monitor.WaitAsync().ConfigureAwait(false);
            }
        });

        var second = Task.Run(async () =>
        {
            using (await _monitor.EnterAsync().ConfigureAwait(false))
            {
                allWaiting.Release();

                await _monitor.WaitAsync().ConfigureAwait(false);
            }
        });

        // Both are inside WaitAsync.
        await allWaiting.WaitAsync().ConfigureAwait(false);
        await allWaiting.WaitAsync().ConfigureAwait(false);

        using (await _monitor.EnterAsync().ConfigureAwait(false))
            _monitor.Pulse();

        var completed = await Task.WhenAny(first, second).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        await completed.ConfigureAwait(false);

        var other = completed == first ? second : first;

        Assert.That(other.IsCompleted, Is.False);

        // Clean up the remaining waiter.
        using (await _monitor.EnterAsync().ConfigureAwait(false))
            _monitor.Pulse();

        await other.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    [Test]
    public async Task Pulse_skips_cancelled_waiters()
    {
        var cancelledWaiting = new SemaphoreSlim(0);
        var liveWaiting = new SemaphoreSlim(0);
        var cts = new CancellationTokenSource();

        var cancelled = Task.Run(async () =>
        {
            using (await _monitor.EnterAsync().ConfigureAwait(false))
            {
                cancelledWaiting.Release();

                await _monitor.WaitAsync(cts.Token).ConfigureAwait(false);
            }
        });

        await cancelledWaiting.WaitAsync().ConfigureAwait(false);

        var live = Task.Run(async () =>
        {
            using (await _monitor.EnterAsync().ConfigureAwait(false))
            {
                liveWaiting.Release();

                await _monitor.WaitAsync().ConfigureAwait(false);
            }
        });

        await liveWaiting.WaitAsync().ConfigureAwait(false);

        // Cancel the first waiter, leaving a dead entry in the queue ahead of the live one.
        await cts.CancelAsync().ConfigureAwait(false);

        Assert.That(
            async () => await cancelled.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false),
            Throws.InstanceOf<TaskCanceledException>());

        // A single Pulse must skip the dead entry and wake the live waiter.
        using (await _monitor.EnterAsync().ConfigureAwait(false))
            _monitor.Pulse();

        await live.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.That(live.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task Cancelled_wait_re_acquires_the_lock_before_throwing()
    {
        var waiting = new SemaphoreSlim(0);
        var cts = new CancellationTokenSource();
        var lockHeldAfterCancellation = false;

        var waiter = Task.Run(async () =>
        {
            using (await _monitor.EnterAsync().ConfigureAwait(false))
            {
                waiting.Release();

                try
                {
                    await _monitor.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // If we're here, WaitAsync re-acquired the lock before throwing.
                    // Verify by checking that a second Enter would block.
                    var probe = _monitor.EnterAsync();
                    lockHeldAfterCancellation = !probe.IsCompleted;
                }
            }
        });

        await waiting.WaitAsync().ConfigureAwait(false);

        await cts.CancelAsync().ConfigureAwait(false);

        await waiter.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.That(lockHeldAfterCancellation, Is.True);
    }

    [Test]
    public async Task Enter_respects_cancellation()
    {
        using var held = await _monitor.EnterAsync().ConfigureAwait(false);

        var cts = new CancellationTokenSource();

        await cts.CancelAsync().ConfigureAwait(false);

        Assert.That(
            async () => await _monitor.EnterAsync(cts.Token).ConfigureAwait(false),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Wait_without_enter_throws()
    {
        Assert.That(
            async () => await _monitor.WaitAsync().ConfigureAwait(false),
            Throws.InstanceOf<SemaphoreFullException>());
    }
}
