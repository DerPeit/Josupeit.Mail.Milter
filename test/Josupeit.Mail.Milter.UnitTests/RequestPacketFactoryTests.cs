// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using NUnit.Framework;
using ProtocolFamily = Josupeit.Mail.Milter.Link.ProtocolFamily;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class RequestPacketFactoryTests
{
    [Test]
    public void Duplicate_macro_name_keeps_the_last_definition()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.Macro,
            MacroPayload(CommandCode.Connect, "j", "first", "j", "second"));

        var packet = (DefineMacroRequestPacket)factory.CreateRequestPacket(frame);

        Assert.That(packet.Macros["j"], Is.EqualTo("second"));
    }

    [Test]
    public void Distinct_macro_names_are_all_kept()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.Macro,
            MacroPayload(CommandCode.Connect, "j", "mail.example.com", "_", "client"));

        var packet = (DefineMacroRequestPacket)factory.CreateRequestPacket(frame);

        Assert.Multiple(() =>
        {
            Assert.That(packet.Macros["j"], Is.EqualTo("mail.example.com"));
            Assert.That(packet.Macros["_"], Is.EqualTo("client"));
        });
    }

    [Test]
    public void Connect_over_ip_yields_an_ip_endpoint()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.Connect,
            ConnectPayload("mail.example.com", ProtocolFamily.Ip4, port: 25, addressText: "192.0.2.1"));

        var packet = (ConnectRequestPacket)factory.CreateRequestPacket(frame);

        Assert.Multiple(() =>
        {
            Assert.That(packet.HostName, Is.EqualTo("mail.example.com"));
            Assert.That(packet.EndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 25)));
        });
    }

    [Test]
    public void Connect_over_a_unix_socket_yields_a_unix_endpoint()
    {
        var factory = new RequestPacketFactory();

        var path = "/var/spool/postfix/public/cleanup";

        var frame = CreateFrame(
            CommandCode.Connect,
            ConnectPayload("localhost", ProtocolFamily.UnixSocket, port: 0, addressText: path));

        var packet = (ConnectRequestPacket)factory.CreateRequestPacket(frame);

        Assert.That(packet.EndPoint, Is.EqualTo(new UnixDomainSocketEndPoint(path)));
    }

    [Test]
    public void Connect_with_an_unknown_family_yields_no_endpoint()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.Connect,
            ConnectPayload("localhost", ProtocolFamily.Unknown, port: 0, addressText: null));

        var packet = (ConnectRequestPacket)factory.CreateRequestPacket(frame);

        Assert.Multiple(() =>
        {
            Assert.That(packet.HostName, Is.EqualTo("localhost"));
            Assert.That(packet.EndPoint, Is.Null);
        });
    }

    [Test]
    public void Connect_with_an_unparsable_address_yields_no_endpoint()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.Connect,
            ConnectPayload("localhost", ProtocolFamily.Ip4, port: 25, addressText: "unknown"));

        var packet = (ConnectRequestPacket)factory.CreateRequestPacket(frame);

        Assert.That(packet.EndPoint, Is.Null);
    }

    [Test]
    public void Header_values_preserve_bytes_above_the_ascii_range()
    {
        var factory = new RequestPacketFactory();

        var value = new byte[] { 0xE9, 0xFF };

        var payload = new List<byte>();

        payload.AddRange(Encoding.Latin1.GetBytes("Subject"));
        payload.Add(0);
        payload.AddRange(value);
        payload.Add(0);

        var frame = CreateFrame(CommandCode.Header, payload.ToArray());

        var packet = (HeaderRequestPacket)factory.CreateRequestPacket(frame);

        Assert.Multiple(() =>
        {
            Assert.That(packet.Name, Is.EqualTo("Subject"));
            Assert.That(Encoding.Latin1.GetBytes(packet.Value), Is.EqualTo(value));
        });
    }

    [Test]
    public void Unterminated_string_is_rejected_as_malformed()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(CommandCode.Helo, Encoding.Latin1.GetBytes("localhost"));

        Assert.That(
            () => factory.CreateRequestPacket(frame),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Option_negotiation_version_with_the_high_bit_set_is_rejected_as_unsupported()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.OptionNegotiation,
            OptionNegotiationPayload(version: 0xFFFFFFFF, actions: 0, events: 0));

        Assert.That(
            () => factory.CreateRequestPacket(frame),
            Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public void Option_negotiation_parses_a_plausible_version()
    {
        var factory = new RequestPacketFactory();

        var frame = CreateFrame(
            CommandCode.OptionNegotiation,
            OptionNegotiationPayload(version: 6, actions: 0, events: 0));

        var packet = (OptionNegotiationRequestPacket)factory.CreateRequestPacket(frame);

        Assert.That(packet.MtaProtocolVersion, Is.EqualTo(new Version(6, 0)));
    }

    [Test]
    public void A_failing_body_chunk_packet_releases_the_frame_buffer()
    {
        var memoryOwner = new TrackingMemoryOwner(size: 16);
        var factory = new ThrowingBodyChunkFactory();

        // A body chunk is the one command whose buffer the factory hands to the packet rather than
        // disposing itself, so it is the only one that can leak when construction fails.
        var frame = new RequestFrame(CommandCode.BodyChunk, memoryOwner, new Range(start: 0, end: 16));

        Assert.That(
            () => factory.CreateRequestPacket(frame),
            Throws.InstanceOf<InvalidOperationException>());

        Assert.That(memoryOwner.IsDisposed, Is.True);
    }

    private static RequestFrame CreateFrame(CommandCode commandCode, byte[] data)
    {
        var owner = MemoryPool<byte>.Shared.Rent(minBufferSize: Math.Max(data.Length, 1));

        data.CopyTo(owner.Memory.Span);

        return new RequestFrame(commandCode, owner, new Range(start: 0, end: data.Length));
    }

    private static byte[] ConnectPayload(string hostName, ProtocolFamily family, ushort port, string? addressText)
    {
        var bytes = new List<byte>();

        bytes.AddRange(Encoding.Latin1.GetBytes(hostName));
        bytes.Add(0);
        bytes.Add((byte)family);

        // SMFIA_UNKNOWN omits both trailing fields.
        if (addressText == null)
            return bytes.ToArray();

        var portBytes = new byte[2];

        BinaryPrimitives.WriteUInt16BigEndian(portBytes, port);

        bytes.AddRange(portBytes);
        bytes.AddRange(Encoding.Latin1.GetBytes(addressText));
        bytes.Add(0);

        return bytes.ToArray();
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

    private sealed class ThrowingBodyChunkFactory : RequestPacketFactory
    {
        protected override BodyChunkRequestPacket CreateBodyChunkRequestPacket(IMemoryOwner<byte>? memory, Range dataRange)
        {
            throw new InvalidOperationException("Malformed packet.");
        }
    }

    private sealed class TrackingMemoryOwner : IMemoryOwner<byte>
    {
        private readonly byte[] _array;

        public TrackingMemoryOwner(int size)
        {
            _array = new byte[size];
        }

        public bool IsDisposed { get; private set; }

        public Memory<byte> Memory => _array;

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
