using System.Text;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ResponseFrameFactoryTests
{
    [Test]
    public void Quarantining_carries_the_reason()
    {
        // Arrange
        var packet = new QuarantineMessageResponsePacket { Reason = "suspicious" };

        // Act
        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        // Assert
        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.QuarantineMessage));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("suspicious\0")));
    }

    private static byte[] Latin1(string text)
    {
        return Encoding.Latin1.GetBytes(text);
    }

    private static byte[] GetData(ResponseFrame frame)
    {
        return frame.Memory is null ? [] : frame.Memory.Memory[frame.DataRange].ToArray();
    }
}
