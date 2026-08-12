// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// Reads the pieces a request frame is made of off the transport.
/// </summary>
/// <remarks>
/// The bottom of the stack, and the only place that knows the byte order: a frame is a big-endian
/// length prefix, then one command byte, then that many bytes less one of data. Substitute an
/// implementation to read frames from something other than a socket.
/// </remarks>
public interface ILinkReader
{
    /// <summary>
    /// Reads a big-endian unsigned 32-bit integer, which on this link is a frame's length prefix.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The value read.</returns>
    ValueTask<uint> ReadUInt32(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the single byte identifying what the frame is.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The command code read.</returns>
    ValueTask<CommandCode> ReadCommandCode(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the payload that follows the command byte.
    /// </summary>
    /// <param name="length">
    /// How many bytes to read. This comes from the length prefix, which the peer controls, so an
    /// implementation must have bounded it before getting here.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// The rented memory holding the payload and the range within it that the payload occupies, or a
    /// <see langword="null"/> owner when <paramref name="length"/> is zero. The caller owns what comes
    /// back and has to dispose it.
    /// </returns>
    ValueTask<(IMemoryOwner<byte>? Memory, Range DataRange)> ReadData(uint length, CancellationToken cancellationToken);
}
