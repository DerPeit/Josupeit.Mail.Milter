// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Josupeit.Mail.Milter.Link;
using ProtocolFamily = Josupeit.Mail.Milter.Link.ProtocolFamily;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Builds typed request packets from the frames the MTA sent.
/// </summary>
/// <remarks>
/// <para>
/// This is where attacker-controlled bytes become objects, so every read is bounds-checked: the
/// <c>TryGetNext*</c> helpers all return <see langword="false"/> rather than reading past the end, and a
/// payload that runs out mid-field is rejected instead of half-parsed.
/// </para>
/// <para>
/// Each packet kind has its own <see langword="protected virtual"/> method, so a derived factory can
/// change how one of them is read without touching the rest.
/// </para>
/// </remarks>
public class RequestPacketFactory : IRequestPacketFactory
{
    private static readonly IReadOnlyDictionary<string, string> _emptyMacros = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(capacity: 0));

    /// <inheritdoc />
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
                CommandCode.MessageData => CreateMessageDataRequestPacket(frame.Data),
                CommandCode.UnknownCommand => CreateUnknownCommandRequestPacket(frame.Data),
                CommandCode.QuitForNewConnection => CreateQuitForNewConnectionRequestPacket(frame.Data),

                _ => throw new ArgumentOutOfRangeException(nameof(frame))
            };
        }
        catch
        {
            // Body chunks hand their pooled buffer to the packet, which is why the finally below
            // deliberately leaves them alone. If constructing that packet threw, the handover never
            // happened and the rent would leak.
            if (frame.CommandCode == CommandCode.BodyChunk)
                frame.Dispose();

            throw;
        }
        finally
        {
            if (frame.CommandCode != CommandCode.BodyChunk)
                frame.Dispose();
        }
    }

    /// <summary>
    /// Builds an <see cref="AbortRequestPacket"/> from the frame's payload. Override to change how it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    protected virtual AbortRequestPacket CreateAbortRequestPacket(ReadOnlyMemory<byte> data)
    {
        return AbortRequestPacket.Instance;
    }

    /// <summary>
    /// Builds a <see cref="BodyChunkRequestPacket"/>, handing it the payload memory rather than copying it.
    /// </summary>
    /// <param name="memory">The rented memory holding the chunk. Ownership passes to the packet.</param>
    /// <param name="dataRange">The range within <paramref name="memory"/> that the chunk occupies.</param>
    /// <returns>The packet.</returns>
    protected virtual BodyChunkRequestPacket CreateBodyChunkRequestPacket(IMemoryOwner<byte>? memory, Range dataRange)
    {
        var packet = new BodyChunkRequestPacket(memory, dataRange);

        return packet;
    }

    /// <summary>
    /// Builds a <see cref="ConnectRequestPacket"/> from the frame's payload. Override to change how it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed Connect frame.</exception>
    protected virtual ConnectRequestPacket CreateConnectRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;

        var hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var hostName)
                                & TryGetNextChar(ref remaining, out var family);

        EnsureHasReadEverything(hasReadEverything);

        var protocolFamily = (ProtocolFamily)family;

        if (protocolFamily is not (ProtocolFamily.Unknown or ProtocolFamily.UnixSocket or ProtocolFamily.Ip4 or ProtocolFamily.Ip6))
            throw new InvalidOperationException("Protocol violation: Unrecognized family code in connect packet.");

        EndPoint? endPoint = null;

        // SMFIA_UNKNOWN carries neither port nor address; every other family carries both.
        if (protocolFamily != ProtocolFamily.Unknown)
        {
            hasReadEverything = TryGetNextUInt16(ref remaining, out var port)
                                & TryGetNextStringVariableLength(ref remaining, out var addressText);

            EnsureHasReadEverything(hasReadEverything);

            endPoint = CreateEndPoint(protocolFamily, port, addressText!);
        }

        var packet = new ConnectRequestPacket
        {
            HostName = hostName!,
            EndPoint = endPoint,
        };

        return packet;
    }

    /// <summary>
    /// Builds a <see cref="DefineMacroRequestPacket"/> from the frame's payload. Override to change how it is
    /// read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed DefineMacro
    /// frame.</exception>
    protected virtual DefineMacroRequestPacket CreateDefineMacroRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;
        var hasReadEverything = TryGetNextChar(ref remaining, out var commandCode);

        EnsureHasReadEverything(hasReadEverything);

        Dictionary<string, string>? macros = null;

        while (TryGetNextStringVariableLength(ref remaining, out var macroName))
        {
            hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var macroValue);

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

    /// <summary>
    /// Builds an <see cref="EndOfBodyRequestPacket"/> from the frame's payload. Override to change how it is
    /// read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    protected virtual EndOfBodyRequestPacket CreateEndOfBodyRequestPacket(ReadOnlyMemory<byte> data)
    {
        return EndOfBodyRequestPacket.Instance;
    }

    /// <summary>
    /// Builds an <see cref="EndOfHeadersRequestPacket"/> from the frame's payload. Override to change how it is
    /// read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    protected virtual EndOfHeadersRequestPacket CreateEndOfHeadersRequestPacket(ReadOnlyMemory<byte> data)
    {
        return EndOfHeadersRequestPacket.Instance;
    }

    /// <summary>
    /// Builds a <see cref="MessageDataRequestPacket"/> from the frame's payload. Override to change how it is
    /// read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    protected virtual MessageDataRequestPacket CreateMessageDataRequestPacket(ReadOnlyMemory<byte> data)
    {
        return MessageDataRequestPacket.Instance;
    }

    /// <summary>
    /// Builds an <see cref="UnknownCommandRequestPacket"/> from the frame's payload. Override to change how it
    /// is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed Unknown frame.</exception>
    protected virtual UnknownCommandRequestPacket CreateUnknownCommandRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;
        var hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var commandText);

        EnsureHasReadEverything(hasReadEverything);

        var packet = new UnknownCommandRequestPacket
        {
            CommandText = commandText!,
        };

        return packet;
    }

    /// <summary>
    /// Builds a <see cref="HeaderRequestPacket"/> from the frame's payload. Override to change how it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed Header frame.</exception>
    protected virtual HeaderRequestPacket CreateHeaderRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;

        var hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var headerName)
                                & TryGetNextStringVariableLength(ref remaining, out var headerValue);

        EnsureHasReadEverything(hasReadEverything);

        var packet = new HeaderRequestPacket
        {
            Name = headerName!,
            Value = headerValue!,
        };

        return packet;
    }

    /// <summary>
    /// Builds a <see cref="HeloRequestPacket"/> from the frame's payload. Override to change how it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed Helo frame.</exception>
    protected virtual HeloRequestPacket CreateHeloRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;
        var hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var heloString);

        EnsureHasReadEverything(hasReadEverything);

        var packet = new HeloRequestPacket
        {
            HeloString = heloString!,
        };

        return packet;
    }

    /// <summary>
    /// Builds an <see cref="OptionNegotiationRequestPacket"/> from the frame's payload. Override to change how
    /// it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed OptionNegotiation
    /// frame.</exception>
    protected virtual OptionNegotiationRequestPacket CreateOptionNegotiationRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;

        var hasReadEverything = TryGetNextUInt32(ref remaining, out var version)
                                & TryGetNextUInt32(ref remaining, out var actions)
                                & TryGetNextUInt32(ref remaining, out var events);

        EnsureHasReadEverything(hasReadEverything);

        // Version only accepts non-negative components, so a version with the high bit set would throw
        // ArgumentOutOfRangeException out of the factory and tear down the session over 13 bytes. No MTA
        // announces such a version - Postfix sends 2 or 6 - so we reject it the way we reject any other
        // capability we cannot honour.
        if (version > int.MaxValue)
            throw new NotSupportedException(
                $"The MTA announced Milter protocol version {version}, which this Milter does not support.");

        var packet = new OptionNegotiationRequestPacket
        {
            MtaProtocolVersion = new Version((int)version, 0),
            MtaSupportedActions = (ActionDiscriminator)actions,
            MtaSupportedContentBypass = (ContentBypassDiscriminator)events,
        };

        return packet;
    }

    /// <summary>
    /// Builds a <see cref="QuitRequestPacket"/> from the frame's payload. Override to change how it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    protected virtual QuitRequestPacket CreateQuitRequestPacket(ReadOnlyMemory<byte> data)
    {
        return QuitRequestPacket.Instance;
    }

    /// <summary>
    /// Builds a <see cref="QuitForNewConnectionRequestPacket"/> from the frame's payload. Override to change
    /// how it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    protected virtual QuitForNewConnectionRequestPacket CreateQuitForNewConnectionRequestPacket(ReadOnlyMemory<byte> data)
    {
        return QuitForNewConnectionRequestPacket.Instance;
    }

    /// <summary>
    /// Builds a <see cref="RecipientRequestPacket"/> from the frame's payload. Override to change how it is
    /// read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed Recipient
    /// frame.</exception>
    protected virtual RecipientRequestPacket CreateRecipientRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;
        var hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var recipient);

        EnsureHasReadEverything(hasReadEverything);

        var eSmtpArguments = ReadESmtpArguments(ref remaining);

        var packet = new RecipientRequestPacket
        {
            Recipient = recipient!,
            ESmtpArguments = eSmtpArguments,
        };

        return packet;
    }

    /// <summary>
    /// Builds a <see cref="SenderRequestPacket"/> from the frame's payload. Override to change how it is read.
    /// </summary>
    /// <param name="data">The frame's payload.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="InvalidOperationException">The payload is not a well-formed Sender frame.</exception>
    protected virtual SenderRequestPacket CreateSenderRequestPacket(ReadOnlyMemory<byte> data)
    {
        var remaining = data.Span;
        var hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var sender);

        EnsureHasReadEverything(hasReadEverything);

        var eSmtpArguments = ReadESmtpArguments(ref remaining);

        var packet = new SenderRequestPacket
        {
            Sender = sender!,
            ESmtpArguments = eSmtpArguments,
        };

        return packet;
    }

    /// <summary>
    /// Whether any bytes are left to read.
    /// </summary>
    /// <param name="remaining">The unread remainder of the payload.</param>
    /// <returns><see langword="true"/> when at least one byte is left.</returns>
    protected static bool HasNext(ReadOnlySpan<byte> remaining)
    {
        return !remaining.IsEmpty;
    }

    /// <summary>
    /// Takes a fixed number of bytes off the front of the payload.
    /// </summary>
    /// <param name="remaining">The unread remainder, advanced past what was taken.</param>
    /// <param name="length">How many bytes to take.</param>
    /// <param name="value">The bytes taken, or empty when there were too few.</param>
    /// <returns><see langword="false"/> when fewer than <paramref name="length"/> bytes are left.</returns>
    protected static bool TryGetNextBytes(ref ReadOnlySpan<byte> remaining, int length, out ReadOnlySpan<byte> value)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, message: null);

        if (remaining.Length < length)
        {
            value = default;

            return false;
        }

        value = remaining[..length];
        remaining = remaining[length..];

        return true;
    }

    /// <summary>
    /// Takes a single byte off the front of the payload and reads it as a character.
    /// </summary>
    /// <param name="remaining">The unread remainder, advanced past what was taken.</param>
    /// <param name="value">The character read, or the default when nothing was left.</param>
    /// <returns><see langword="false"/> when the payload is exhausted.</returns>
    protected static bool TryGetNextChar(ref ReadOnlySpan<byte> remaining, out char value)
    {
        if (!TryGetNextBytes(ref remaining, length: 1, out var bytes))
        {
            value = default;

            return false;
        }

        value = (char)bytes[0];

        return true;
    }

    /// <summary>
    /// Takes a string of known length off the front of the payload.
    /// </summary>
    /// <param name="remaining">The unread remainder, advanced past what was taken.</param>
    /// <param name="length">How many bytes the string occupies.</param>
    /// <param name="value">The string read, or <see langword="null"/> when there were too few bytes.</param>
    /// <returns><see langword="false"/> when fewer than <paramref name="length"/> bytes are left.</returns>
    protected static bool TryGetNextStringFixedLength(ref ReadOnlySpan<byte> remaining, int length, [NotNullWhen(true)] out string? value)
    {
        if (!TryGetNextBytes(ref remaining, length, out var bytes))
        {
            value = null;

            return false;
        }

        value = Encoding.Latin1.GetString(bytes);

        return true;
    }

    /// <summary>
    /// Takes a NUL-terminated string off the front of the payload.
    /// </summary>
    /// <remarks>
    /// The protocol's usual string form. A payload whose last string is unterminated is malformed, which is why
    /// a missing terminator fails rather than yielding the rest.
    /// </remarks>
    /// <param name="remaining">The unread remainder, advanced past the string and its terminator.</param>
    /// <param name="value">The string read, or <see langword="null"/> when no terminator was found.</param>
    /// <returns><see langword="false"/> when the payload holds no NUL terminator.</returns>
    protected static bool TryGetNextStringVariableLength(ref ReadOnlySpan<byte> remaining, [NotNullWhen(true)] out string? value)
    {
        var terminator = remaining.IndexOf((byte)0);

        if (terminator < 0)
        {
            value = null;

            return false;
        }

        value = Encoding.Latin1.GetString(remaining[..terminator]);
        remaining = remaining[(terminator + 1)..];

        return true;
    }

    /// <summary>
    /// Takes a big-endian unsigned 16-bit integer off the front of the payload.
    /// </summary>
    /// <param name="remaining">The unread remainder, advanced past what was taken.</param>
    /// <param name="value">The value read, or zero when there were too few bytes.</param>
    /// <returns><see langword="false"/> when fewer than two bytes are left.</returns>
    protected static bool TryGetNextUInt16(ref ReadOnlySpan<byte> remaining, out ushort value)
    {
        if (!TryGetNextBytes(ref remaining, sizeof(ushort), out var bytes))
        {
            value = default;

            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(bytes);

        return true;
    }

    /// <summary>
    /// Takes a big-endian unsigned 32-bit integer off the front of the payload.
    /// </summary>
    /// <param name="remaining">The unread remainder, advanced past what was taken.</param>
    /// <param name="value">The value read, or zero when there were too few bytes.</param>
    /// <returns><see langword="false"/> when fewer than four bytes are left.</returns>
    protected static bool TryGetNextUInt32(ref ReadOnlySpan<byte> remaining, out uint value)
    {
        if (!TryGetNextBytes(ref remaining, sizeof(uint), out var bytes))
        {
            value = default;

            return false;
        }

        value = BinaryPrimitives.ReadUInt32BigEndian(bytes);

        return true;
    }

    private static EndPoint? CreateEndPoint(ProtocolFamily protocolFamily, ushort port, string addressText)
    {
        // UnixDomainSocketEndPoint rejects a path outside 1 to 108 bytes, and the path comes off the wire.
        // We deliberately let that throw: a socket path we cannot represent is not something we can
        // meaningfully carry forward, and an MTA does not send one.
        if (protocolFamily == ProtocolFamily.UnixSocket)
            return new UnixDomainSocketEndPoint(addressText);

        // An internet address may arrive as the literal "unknown" when the MTA has none - Postfix sends
        // that for XCLIENT ADDR=[UNAVAILABLE]. That is the MTA reporting no address, not a parse failure,
        // and it is reported to the filter the same way an unknown protocol family is.
        return IPAddress.TryParse(addressText, out var address)
            ? new IPEndPoint(address, port)
            : null;
    }

    private static IReadOnlyList<string> ReadESmtpArguments(ref ReadOnlySpan<byte> remaining)
    {
        List<string>? eSmtpArguments = null;

        while (HasNext(remaining))
        {
            var hasReadEverything = TryGetNextStringVariableLength(ref remaining, out var eSmtpArgument);

            EnsureHasReadEverything(hasReadEverything);

            eSmtpArguments ??= new();

            eSmtpArguments.Add(eSmtpArgument!);
        }

        return eSmtpArguments ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    private static void EnsureHasReadEverything(bool hasReadEverything)
    {
        if (hasReadEverything)
            return;

        throw new InvalidOperationException("Protocol violation: Request frame was malformed.");
    }
}
