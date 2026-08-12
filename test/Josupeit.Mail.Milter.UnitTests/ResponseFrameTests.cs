// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ResponseFrameTests
{
    [Test]
    public void Waiting_for_a_frame_that_never_reaches_the_link_times_out()
    {
        var frame = new ResponseFrame(ResponseCode.ContinueProcessing);

        Assert.That(
            async () => await frame.WaitAsync(TimeSpan.FromMilliseconds(50), TimeProvider.System, CancellationToken.None),
            Throws.InstanceOf<TimeoutException>());
    }

    [Test]
    public async Task Waiting_for_a_completed_frame_returns()
    {
        var frame = new ResponseFrame(ResponseCode.ContinueProcessing);

        frame.SetCompleted();

        await frame.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
    }

    [Test]
    public void Waiting_for_a_frame_observes_cancellation()
    {
        var frame = new ResponseFrame(ResponseCode.ContinueProcessing);

        using var stopper = new CancellationTokenSource();

        stopper.Cancel();

        Assert.That(
            async () => await frame.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, stopper.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
