using System.Linq;
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

    [Test]
    public void Replying_doubles_percent_signs_in_the_text()
    {
        // Arrange
        var packet = new ReplyWithCodeResponsePacket { SmtpCode = "550", Text = "5.7.1 blocked 100% of the time" };

        // Act
        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        // Assert
        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.ReplyWithCode));
        Assert.That(GetData(frame), Is.EqualTo(Latin1("550 5.7.1 blocked 100%% of the time\0")));
    }

    [Test]
    public void Replying_leaves_a_text_without_percent_signs_untouched()
    {
        // Arrange
        var packet = new ReplyWithCodeResponsePacket { SmtpCode = "451", Text = "try again later" };

        // Act
        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        // Assert
        Assert.That(GetData(frame), Is.EqualTo(Latin1("451 try again later\0")));
    }

    [Test]
    public void Replying_doubles_adjacent_and_trailing_percent_signs()
    {
        // Arrange
        var packet = new ReplyWithCodeResponsePacket { SmtpCode = "550", Text = "%%%" };

        // Act
        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        // Assert
        Assert.That(GetData(frame), Is.EqualTo(Latin1("550 %%%%%%\0")));
    }

    [Test]
    public void Changing_a_header_counts_occurrences_from_one_on_the_wire()
    {
        // Arrange
        var packet = new ChangeOrDeleteHeaderResponsePacket { Index = 0, Name = "Received", Value = "redacted" };

        // Act
        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        // Assert
        Assert.That(frame.ResponseCode, Is.EqualTo(ResponseCode.ChangeOrDeleteHeader));
        Assert.That(GetData(frame), Is.EqualTo(new byte[] { 0, 0, 0, 1 }.Concat(Latin1("Received\0redacted\0"))));
    }

    [Test]
    public void Deleting_a_header_keeps_the_index_offset()
    {
        // Arrange
        var packet = new ChangeOrDeleteHeaderResponsePacket { Index = 2, Name = "Received", Value = string.Empty };

        // Act
        using var frame = new ResponseFrameFactory().CreateResponseFrame(packet);

        // Assert
        Assert.That(GetData(frame), Is.EqualTo(new byte[] { 0, 0, 0, 3 }.Concat(Latin1("Received\0\0"))));
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
