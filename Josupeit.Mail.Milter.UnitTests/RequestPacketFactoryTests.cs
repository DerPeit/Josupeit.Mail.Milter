using System;
using System.Buffers;
using System.Buffers.Binary;
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

    [Test]
    public void Option_negotiation_version_with_the_high_bit_set_is_rejected_as_unsupported()
    {
        // Arrange
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.OptionNegotiation,
            OptionNegotiationPayload(version: 0xFFFFFFFF, actions: 0, events: 0));

        // Act
        // Assert
        Assert.That(
            () => factory.CreateRequestPacket(frame),
            Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public void Option_negotiation_parses_a_plausible_version()
    {
        // Arrange
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.OptionNegotiation,
            OptionNegotiationPayload(version: 6, actions: 0, events: 0));

        // Act
        var packet = (OptionNegotiationRequestPacket)factory.CreateRequestPacket(frame);

        // Assert
        Assert.That(packet.MtaProtocolVersion, Is.EqualTo(new Version(major: 6, minor: 0)));
    }

    private static RequestFrame CreateFrame(CommandCode commandCode, byte[] data)
    {
        var owner = MemoryPool<byte>.Shared.Rent(minBufferSize: Math.Max(data.Length, 1));

        data.CopyTo(owner.Memory.Span);

        return new RequestFrame(commandCode, owner, new Range(start: 0, end: data.Length));
    }

    private static byte[] OptionNegotiationPayload(uint version, uint actions, uint events)
    {
        var bytes = new byte[12];

        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(start: 0, length: 4), version);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(start: 4, length: 4), actions);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(start: 8, length: 4), events);

        return bytes;
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
