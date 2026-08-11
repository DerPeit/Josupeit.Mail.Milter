using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Josupeit.Mail.Milter.Infrastructure;
using Josupeit.Mail.Milter.Link;
using ProtocolFamily = Josupeit.Mail.Milter.Link.ProtocolFamily;

namespace Josupeit.Mail.Milter.Protocol;

public class RequestPacketFactory : IRequestPacketFactory
{
    private static readonly IReadOnlyDictionary<string, string> _emptyMacros = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(capacity: 0));

    public RequestPacket CreateRequestPacket(RequestFrame frame)
    {
        try
        {
            return frame.CommandCode switch
            {
                CommandCode.Abort => CreateAbortRequestPacket(frame.Data),
                CommandCode.BodyChunk => CreateBodyChunkRequestPacket(frame.Memory, frame.DataRange),
                CommandCode.Connect => CreateConnectRequestPacket(frame.Data),
                CommandCode.Macro => CreateDefineMacroRequestPacket(frame.Data),
                CommandCode.EndOfBody => CreateEndOfBodyRequestPacket(frame.Data),
                CommandCode.Helo => CreateHeloRequestPacket(frame.Data),
                CommandCode.Header => CreateHeaderRequestPacket(frame.Data),
                CommandCode.Sender => CreateSenderRequestPacket(frame.Data),
                CommandCode.EndOfHeaders => CreateEndOfHeadersRequestPacket(frame.Data),
                CommandCode.OptionNegotiation => CreateOptionNegotiationRequestPacket(frame.Data),
                CommandCode.Recipient => CreateRecipientRequestPacket(frame.Data),
                CommandCode.Quit => CreateQuitRequestPacket(frame.Data),

                _ => throw new ArgumentOutOfRangeException(nameof(frame))
            };
        }
        finally
        {
            if (frame.CommandCode != CommandCode.BodyChunk)
                frame.Dispose();
        }
    }

    protected virtual AbortRequestPacket CreateAbortRequestPacket(ReadOnlyMemory<byte> data)
    {
        return AbortRequestPacket.Instance;
    }

    protected virtual BodyChunkRequestPacket CreateBodyChunkRequestPacket(IMemoryOwner<byte>? memory, Range dataRange)
    {
        var packet = new BodyChunkRequestPacket(memory, dataRange);

        return packet;
    }

    protected virtual ConnectRequestPacket CreateConnectRequestPacket(ReadOnlyMemory<byte> data)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(data));

        var hasReadEverything = TryGetNextStringVariableLength(reader, out var hostName)
                                & TryGetNextChar(reader, out var family);

        EnsureHasReadEverything(hasReadEverything);

        var addressFamily = (ProtocolFamily)family switch
        {
            ProtocolFamily.Unknown => AddressFamily.Unknown,
            ProtocolFamily.UnixSocket => AddressFamily.Unix,
            ProtocolFamily.Ip4 => AddressFamily.InterNetwork,
            ProtocolFamily.Ip6 => AddressFamily.InterNetworkV6,

            _ => throw new InvalidOperationException("Protocol violation: Unrecognized family code in connect packet."),
        };

        ushort port = 0;
        var address = IPAddress.None;

        // SMFIA_UNKNOWN carries neither port nor address; every other family carries both.
        // For SMFIA_UNIX the address is a socket path, and an internet address may be the
        // literal "unknown" (Postfix XCLIENT ADDR=[UNAVAILABLE]) - neither is an IPAddress.
        if (addressFamily != AddressFamily.Unknown)
        {
            hasReadEverything = TryGetNextUInt16(reader, out port)
                                & TryGetNextStringVariableLength(reader, out var addressText);

            EnsureHasReadEverything(hasReadEverything);

            if (addressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                address = IPAddress.TryParse(addressText, out var parsedAddress) ? parsedAddress : IPAddress.None;
        }

        var packet = new ConnectRequestPacket
        {
            HostName = hostName!,
            Family = addressFamily,
            Port = port,
            Address = address,
        };

        return packet;
    }

    protected virtual DefineMacroRequestPacket CreateDefineMacroRequestPacket(ReadOnlyMemory<byte> data)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(data));
        var hasReadEverything = TryGetNextChar(reader, out var commandCode);

        EnsureHasReadEverything(hasReadEverything);

        Dictionary<string, string>? macros = null;

        while (TryGetNextStringVariableLength(reader, out var macroName))
        {
            hasReadEverything = TryGetNextStringVariableLength(reader, out var macroValue);

            EnsureHasReadEverything(hasReadEverything);

            macros ??= new();

            // An MTA may redefine a macro within one packet, so the last definition wins. Using
            // Dictionary.Add here would throw and tear down the session over ~20 bytes on the wire.
            macros[macroName] = macroValue!;
        }

        var packet = new DefineMacroRequestPacket
        {
            CommandCode = (CommandCode)commandCode,
            Macros = macros?.AsReadOnly() ?? _emptyMacros,
        };

        return packet;
    }

    protected virtual EndOfBodyRequestPacket CreateEndOfBodyRequestPacket(ReadOnlyMemory<byte> data)
    {
        return EndOfBodyRequestPacket.Instance;
    }

    protected virtual EndOfHeadersRequestPacket CreateEndOfHeadersRequestPacket(ReadOnlyMemory<byte> data)
    {
        return EndOfHeadersRequestPacket.Instance;
    }

    protected virtual HeaderRequestPacket CreateHeaderRequestPacket(ReadOnlyMemory<byte> data)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(data));

        var hasReadEverything = TryGetNextStringVariableLength(reader, out var headerName)
                                & TryGetNextStringVariableLength(reader, out var headerValue);

        EnsureHasReadEverything(hasReadEverything);

        var packet = new HeaderRequestPacket
        {
            Name = headerName!,
            Value = headerValue!,
        };

        return packet;
    }

    protected virtual HeloRequestPacket CreateHeloRequestPacket(ReadOnlyMemory<byte> data)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(data));
        var hasReadEverything = TryGetNextStringVariableLength(reader, out var heloString);

        EnsureHasReadEverything(hasReadEverything);

        var packet = new HeloRequestPacket
        {
            HeloString = heloString!,
        };

        return packet;
    }
    protected virtual OptionNegotiationRequestPacket CreateOptionNegotiationRequestPacket(ReadOnlyMemory<byte> data)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(data));

        var hasReadEverything = TryGetNextUInt32(reader, out var version)
                                & TryGetNextUInt32(reader, out var actions)
                                & TryGetNextUInt32(reader, out var events);

        EnsureHasReadEverything(hasReadEverything);

        var packet = new OptionNegotiationRequestPacket
        {
            MtaProtocolVersion = new Version((int)version, 0),
            MtaSupportedActions = (ActionDiscriminator)actions,
            MtaSupportedContentBypass = (ContentBypassDiscriminator)events,
        };

        return packet;
    }

    protected virtual QuitRequestPacket CreateQuitRequestPacket(ReadOnlyMemory<byte> data)
    {
        return QuitRequestPacket.Instance;
    }

    protected virtual RecipientRequestPacket CreateRecipientRequestPacket(ReadOnlyMemory<byte> data)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(data));
        var hasReadEverything = TryGetNextStringVariableLength(reader, out var recipient);

        EnsureHasReadEverything(hasReadEverything);

        List<string>? eSmtpArguments = null;

        while (HasNext(reader))
        {
            hasReadEverything = TryGetNextStringVariableLength(reader, out var eSmtpArgument);

            EnsureHasReadEverything(hasReadEverything);

            eSmtpArguments ??= new();
            eSmtpArguments.Add(eSmtpArgument!);
        }

        var packet = new RecipientRequestPacket
        {
            Recipient = recipient!,
            ESmtpArguments = (IReadOnlyList<string>?)eSmtpArguments ?? Array.Empty<string>(),
        };

        return packet;
    }

    protected virtual SenderRequestPacket CreateSenderRequestPacket(ReadOnlyMemory<byte> data)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(data));
        var hasReadEverything = TryGetNextStringVariableLength(reader, out var sender);

        EnsureHasReadEverything(hasReadEverything);

        List<string>? eSmtpArguments = null;

        while (HasNext(reader))
        {
            hasReadEverything = TryGetNextStringVariableLength(reader, out var eSmtpArgument);

            EnsureHasReadEverything(hasReadEverything);

            eSmtpArguments ??= new();
            eSmtpArguments.Add(eSmtpArgument!);
        }

        var packet = new SenderRequestPacket
        {
            Sender = sender!,
            ESmtpArguments = (IReadOnlyList<string>?)eSmtpArguments ?? Array.Empty<string>(),
        };

        return packet;
    }
    protected bool HasNext(PipeReader reader)
    {
        var hasRead = reader.TryRead(out var readResult);

        return hasRead && !readResult.Buffer.IsEmpty;
    }

    protected bool TryGetNextBytes(PipeReader reader, long length, out ReadOnlySequence<byte> value)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, message: null);
        
        while (true)
        {
            var hasRead = reader.TryRead(out var readResult);

            if (!hasRead)
            {
                value = ReadOnlySequence<byte>.Empty;

                return false;
            }

            Debug.Assert(!readResult.IsCanceled);

            value = readResult.Buffer;

            if (value.Length == length)
            {
                reader.AdvanceTo(value.End);

                return true;
            }

            if (value.Length > length)
            {
                value = value.Slice(0, length);

                reader.AdvanceTo(value.End);

                return true;
            }

            reader.AdvanceTo(value.Start, value.End);

            if (readResult.IsCompleted)
                return false;
        }
    }

    protected bool TryGetNextChar(in PipeReader reader, out char value)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        if (TryGetNextBytes(reader, length: 1, out var buffer))
        {
            value = (char)buffer.FirstSpan[0];

            return true;
        }

        value = default;

        return false;
    }

    protected bool TryGetNextStringFixedLength(PipeReader reader, long length, [NotNullWhen(true)] out string? value)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, message: null);

        if (TryGetNextBytes(reader, length, out var buffer))
        {
            value = Encoding.ASCII.GetString(buffer);

            return true;
        }

        value = default;

        return false;
    }

    protected bool TryGetNextStringVariableLength(PipeReader reader, [NotNullWhen(true)] out string? value)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        StringBuilder? sb = null;

        while (true)
        {
            var hasRead = reader.TryRead(out var readResult);

            if (!hasRead)
                break;

            Debug.Assert(!readResult.IsCanceled);

            var offset = 0;

            foreach (var bytes in readResult.Buffer)
                foreach (var @byte in bytes.Span)
                {
                    offset++;

                    if (@byte == 0)
                    {
                        reader.AdvanceTo(readResult.Buffer.GetPosition(offset));
                        value = sb?.ToString() ?? string.Empty;

                        return true;
                    }

                    sb ??= new();
                    sb.Append((char)@byte);
                }

            reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.GetPosition(offset));

            if (readResult.IsCompleted)
                break;
        }

        value = null;

        return false;
    }

    protected bool TryGetNextUInt16(PipeReader reader, out ushort value)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        if (TryGetNextBytes(reader, length: 2, out var buffer))
        {
            value = buffer.AsSpanInvoke(BinaryPrimitives.ReadUInt16BigEndian);

            return true;
        }

        value = default;
            
        return false;
    }

    protected bool TryGetNextUInt32(PipeReader reader, out uint value)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        if (TryGetNextBytes(reader, length: 4, out var buffer))
        {
            value = buffer.AsSpanInvoke(BinaryPrimitives.ReadUInt32BigEndian);

            return true;
        }

        value = default;

        return false;
    }
    private static void EnsureHasReadEverything(bool hasReadEverything)
    {
        if (hasReadEverything)
            return;

        throw new InvalidOperationException("Protocol violation: Request frame was malformed.");
    }
}