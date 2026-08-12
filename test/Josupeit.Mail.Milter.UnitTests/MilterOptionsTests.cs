using System;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class MilterOptionsTests
{
    [Test]
    public void Defaults_are_usable_without_configuration()
    {
        var options = new MilterOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.MaxFrameLength, Is.GreaterThan(65536));
            Assert.That(options.MaxConcurrentSessions, Is.GreaterThan(0));
        });
    }

    [Test]
    public void A_frame_length_of_zero_is_rejected()
    {
        Assert.That(
            () => new MilterOptions { MaxFrameLength = 0 },
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void A_session_limit_below_one_is_rejected()
    {
        Assert.That(
            () => new MilterOptions { MaxConcurrentSessions = 0 },
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
