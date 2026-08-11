using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class RequestPacketFactoryTests
{
    [Test]
    public void Duplicate_macro_name_keeps_the_last_definition()
    {
        // Arrange
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.Macro,
            MacroPayload(CommandCode.Connect, "j", "first", "j", "second"));

        // Act
        var packet = (DefineMacroRequestPacket)factory.CreateRequestPacket(frame);

        // Assert
        Assert.That(packet.Macros["j"], Is.EqualTo("second"));
    }

    [Test]
    public void Distinct_macro_names_are_all_kept()
    {
        // Arrange
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.Macro,
            MacroPayload(CommandCode.Connect, "j", "mail.example.com", "_", "client"));

        // Act
        var packet = (DefineMacroRequestPacket)factory.CreateRequestPacket(frame);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(packet.Macros["j"], Is.EqualTo("mail.example.com"));
            Assert.That(packet.Macros["_"], Is.EqualTo("client"));
        });
    }

    private static RequestFrame CreateFrame(CommandCode commandCode, byte[] data)
    {
        var owner = MemoryPool<byte>.Shared.Rent(minBufferSize: Math.Max(data.Length, 1));

        data.CopyTo(owner.Memory.Span);

        return new RequestFrame(commandCode, owner, new Range(0, data.Length));
    }

    private static byte[] MacroPayload(CommandCode stage, params string[] namesAndValues)
    {
        var bytes = new List<byte> { (byte)stage };

        foreach (var item in namesAndValues)
        {
            bytes.AddRange(Encoding.Latin1.GetBytes(item));
            bytes.Add(0);
        }

        return bytes.ToArray();
    }
}
