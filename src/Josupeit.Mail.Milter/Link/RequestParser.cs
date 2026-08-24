// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// Reads frames off an <see cref="ILinkReader"/> and publishes them to a channel.
/// </summary>
/// <remarks>
/// The reading half of a session, run as its own long-lived task. The channel it writes to is bounded at
/// one, so it stays exactly one frame ahead of the session handling them - enough to overlap reading with
/// handling, not enough for a fast peer to pile up work.
/// </remarks>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The cancellation source belongs to a run, not to the instance: Start creates it and Stop disposes it.")]
public class RequestParser : IRequestParser
{
    private readonly ILinkReader _linkReader;
    private readonly ChannelWriter<RequestFrame> _outputWriter;
    private readonly uint _maxFrameLength;

    private CancellationTokenSource? _stopper;
    private Task? _runner;

    /// <summary>
    /// Creates a parser over a link.
    /// </summary>
    /// <param name="linkReader">The link to read frames from.</param>
    /// <param name="outputWriter">The channel the frames are published to.</param>
    /// <param name="maxFrameLength">
    /// The largest frame to accept, in bytes. See <see cref="MilterOptions.MaxFrameLength"/>.
    /// </param>
    public RequestParser(ILinkReader linkReader, ChannelWriter<RequestFrame> outputWriter, uint maxFrameLength)
    {
        if (maxFrameLength < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFrameLength), maxFrameLength, message: null);

        _linkReader = linkReader ?? throw new ArgumentNullException(nameof(linkReader));
        _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        _maxFrameLength = maxFrameLength;
    }

    /// <inheritdoc />
    public void Start()
    {
        if (_runner != null)
            throw new InvalidOperationException($"{nameof(RequestParser)} cannot be started. It is already running.");

        _stopper = new();
        _runner = Task.Run(ProcessFramesCore, _stopper.Token);
    }

    /// <inheritdoc />
    public async Task Stop()
    {
        if (_runner == null)
            throw new InvalidOperationException($"{nameof(RequestParser)} cannot be stopped. It is not running.");

        await _stopper!.CancelAsync().ConfigureAwait(false);

        try
        {
            await _runner!.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Deliberately discarded.
        }
        finally
        {
            _stopper.Dispose();
            _stopper = null;
            _runner = null;
        }
    }

    /// <summary>
    /// Reads one frame off the link and publishes it.
    /// </summary>
    /// <remarks>
    /// The unit an override changes to read the wire differently, leaving the loop, its cancellation and
    /// its shutdown alone. Ownership of the frame's buffer passes to the channel here: a frame that
    /// cannot be published is disposed rather than dropped, which is why the publish is inside rather
    /// than left to the caller.
    /// </remarks>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>A task that completes once the frame has been published.</returns>
    protected virtual async Task ProcessFrame(CancellationToken cancellationToken)
    {
        var length = await _linkReader.ReadUInt32(cancellationToken).ConfigureAwait(false);

        // The length covers the command byte, so it must be at least 1. Guarding here is what keeps
        // "length - 1" from underflowing to uint.MaxValue and turning a zero-length frame into a 4 GiB
        // read, and what keeps an announced length from sizing an unbounded rent.
        EnsureFrameLengthIsInRange(length);

        var commandCode = await _linkReader.ReadCommandCode(cancellationToken).ConfigureAwait(false);
        var (memory, dataRange) = await _linkReader.ReadData(length - 1, cancellationToken).ConfigureAwait(false);

        var requestFrame = new RequestFrame(commandCode, memory, dataRange);

        try
        {
            await _outputWriter.WriteAsync(requestFrame, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            requestFrame.Dispose();

            throw;
        }
    }

    /// <summary>
    /// The loop the worker runs: reads frames off the link and publishes them until the token is
    /// cancelled or the link ends.
    /// </summary>
    /// <param name="cancellationToken">A token that ends the loop.</param>
    /// <returns>A task that completes when the loop has ended.</returns>
    protected virtual async Task ProcessFrames(CancellationToken cancellationToken)
    {
        Exception? error = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
                await ProcessFrame(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            _outputWriter.Complete(error);
        }
    }

    private void EnsureFrameLengthIsInRange(uint length)
    {
        if (length >= 1 && length <= _maxFrameLength)
            return;

        throw new InvalidOperationException(
            $"Protocol violation: Frame length {length} is outside the accepted range of 1 to {_maxFrameLength} bytes.");
    }

    private Task ProcessFramesCore() => ProcessFrames(_stopper!.Token);
}
