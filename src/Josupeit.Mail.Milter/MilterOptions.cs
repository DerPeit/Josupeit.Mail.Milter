// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter;

/// <summary>
/// The limits a <see cref="Milter{TContext}"/> enforces on what a peer can make it do.
/// </summary>
/// <remarks>
/// Both settings bound something the peer controls, which is why they exist at all rather than being
/// constants. The defaults suit an MTA on the same host; tighten them when the milter is reachable from
/// somewhere less trusted.
/// </remarks>
public sealed record MilterOptions
{
    /// <summary>
    /// The default for <see cref="MaxFrameLength"/>: one mebibyte.
    /// </summary>
    public static readonly uint DefaultMaxFrameLength = 1024 * 1024;

    /// <summary>
    /// The default for <see cref="MaxConcurrentSessions"/>: 1024 connections.
    /// </summary>
    public static readonly int DefaultMaxConcurrentSessions = 1024;

    private readonly uint _maxFrameLength = DefaultMaxFrameLength;
    private readonly int _maxConcurrentSessions = DefaultMaxConcurrentSessions;

    /// <summary>
    /// The upper bound for the length prefix of a single request frame, in bytes.
    /// </summary>
    /// <remarks>
    /// The length prefix is attacker-controlled, so it must be bounded before it is used to size a read
    /// or an allocation. The default sits comfortably above everything Postfix generates: its body chunks
    /// are capped at MILTER_CHUNK_SIZE (65535) and its default header_size_limit is 102400. Lower it if
    /// the MTA is known to be more constrained.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero.</exception>
    public uint MaxFrameLength
    {
        get => _maxFrameLength;

        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1u);

            _maxFrameLength = value;
        }
    }

    /// <summary>
    /// The number of connections that may be served at the same time.
    /// </summary>
    /// <remarks>
    /// Each session holds buffers and pipes for the life of the connection, so an unbounded count lets a
    /// peer that opens connections faster than they complete exhaust the host. Connections accepted past
    /// this limit are closed immediately rather than queued, so the peer finds out at once.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
    public int MaxConcurrentSessions
    {
        get => _maxConcurrentSessions;

        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

            _maxConcurrentSessions = value;
        }
    }
}
