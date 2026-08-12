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
        // Arrange
        var frame = new ResponseFrame(ResponseCode.ContinueProcessing);

        // Act
        // Assert
        Assert.That(
            async () => await frame.WaitAsync(TimeSpan.FromMilliseconds(50), TimeProvider.System, CancellationToken.None),
            Throws.InstanceOf<TimeoutException>());
    }

    [Test]
    public async Task Waiting_for_a_completed_frame_returns()
    {
        // Arrange
        var frame = new ResponseFrame(ResponseCode.ContinueProcessing);

        // Act
        frame.SetCompleted();

        // Assert
        await frame.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
    }

    [Test]
    public void Waiting_for_a_frame_observes_cancellation()
    {
        // Arrange
        var frame = new ResponseFrame(ResponseCode.ContinueProcessing);

        using var stopper = new CancellationTokenSource();

        stopper.Cancel();

        // Act
        // Assert
        Assert.That(
            async () => await frame.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, stopper.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
