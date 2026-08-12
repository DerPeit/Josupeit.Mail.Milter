// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// Writes the pieces a response frame is made of to the transport.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="ILinkReader"/>, and the mirror image of its layout: a big-endian length
/// prefix, then one response byte, then the data. The methods return <see cref="ValueTask"/> rather than
/// <see cref="Task"/> because they are called for every frame and an implementer may want to back them
/// with a pooled <see cref="System.Threading.Tasks.Sources.IValueTaskSource"/>.
/// </remarks>
public interface ILinkWriter
{
    /// <summary>
    /// Writes a big-endian unsigned 32-bit integer, which on this link is a frame's length prefix.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the value has been written.</returns>
    ValueTask WriteUInt32(uint value, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the single byte identifying what the frame is.
    /// </summary>
    /// <param name="responseCode">The code to write.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the code has been written.</returns>
    ValueTask WriteResponseCode(ResponseCode responseCode, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the payload that follows the response byte.
    /// </summary>
    /// <param name="data">The bytes to write. May be empty.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the payload has been written.</returns>
    ValueTask WriteData(ReadOnlySpan<byte> data, CancellationToken cancellationToken);
}
