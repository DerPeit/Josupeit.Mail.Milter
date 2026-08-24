// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// Takes frames off a channel and writes them to an <see cref="ILinkWriter"/>.
/// </summary>
/// <remarks>
/// The writing half of a session, and the mirror of <see cref="RequestParser"/>. Each frame is completed
/// once it has been written, which is what lets the session wait for a response to have actually left
/// rather than merely been queued.
/// </remarks>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The cancellation source belongs to a run, not to the instance: Start creates it and Stop disposes it.")]
public class ResponseFormatter : IResponseFormatter
{
    private readonly ChannelReader<ResponseFrame> _inputReader;
    private readonly ILinkWriter _linkWriter;

    private CancellationTokenSource? _stopper;
    private Task? _runner;

    /// <summary>
    /// Creates a formatter over a link.
    /// </summary>
    /// <param name="inputReader">The channel the frames to write arrive on.</param>
    /// <param name="linkWriter">The link to write them to.</param>
    public ResponseFormatter(ChannelReader<ResponseFrame> inputReader, ILinkWriter linkWriter)
    {
        _inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        _linkWriter = linkWriter ?? throw new ArgumentNullException(nameof(linkWriter));
    }

    /// <inheritdoc />
    public void Start()
    {
        if (_runner != null)
            throw new InvalidOperationException($"{nameof(ResponseFormatter)} cannot be started. It is already running.");

        _stopper = new();
        _runner = Task.Run(ProcessFramesCore, _stopper.Token);
    }

    /// <inheritdoc />
    public async Task Stop()
    {
        if (_runner == null)
            throw new InvalidOperationException($"{nameof(ResponseFormatter)} cannot be stopped. It is not running.");

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
    /// Writes one frame to the link.
    /// </summary>
    /// <remarks>
    /// The unit an override changes to put different bytes on the wire, leaving the loop alone. Telling
    /// the frame it is done is deliberately not part of this: the session waits on that signal, so a
    /// forgetful override would hang it rather than merely write the wrong thing. The loop completes,
    /// cancels or faults the frame around this call whatever it does.
    /// </remarks>
    /// <param name="frame">The frame to write. Its buffer stays owned by the loop.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes once the frame has been written.</returns>
    protected virtual async Task ProcessFrame(ResponseFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Memory == null)
        {
            await _linkWriter.WriteUInt32(sizeof(ResponseCode), cancellationToken).ConfigureAwait(false);
            await _linkWriter.WriteResponseCode(frame.ResponseCode, cancellationToken).ConfigureAwait(false);

            return;
        }

        var data = frame.Memory.Memory[frame.DataRange];
        var frameLength = (uint)(sizeof(ResponseCode) + data.Length);

        await _linkWriter.WriteUInt32(frameLength, cancellationToken).ConfigureAwait(false);
        await _linkWriter.WriteResponseCode(frame.ResponseCode, cancellationToken).ConfigureAwait(false);
        await _linkWriter.WriteData(data.Span, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The loop the worker runs: takes frames off the channel and writes them to the link until the token
    /// is cancelled or the channel completes.
    /// </summary>
    /// <param name="cancellationToken">A token that ends the loop.</param>
    /// <returns>A task that completes when the loop has ended.</returns>
    protected virtual async Task ProcessFrames(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _inputReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                using (frame)
                {
                    try
                    {
                        await ProcessFrame(frame, cancellationToken).ConfigureAwait(false);

                        frame.SetCompleted();
                    }
                    catch (OperationCanceledException)
                    {
                        frame.SetCanceled(cancellationToken);

                        throw;
                    }
                    catch (Exception ex)
                    {
                        frame.SetException(ex);
                    }
                }
            }
        }
        finally
        {
            try
            {
                await foreach (var orphanedFrame in _inputReader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
                    try
                    {
                        using (orphanedFrame)
                            orphanedFrame.SetCanceled(CancellationToken.None);
                    }
                    catch
                    {
                        // Deliberately discarded.
                    }
            }
            catch
            {
                // Deliberately discarded.
            }
        }
    }

    private Task ProcessFramesCore() => ProcessFrames(_stopper!.Token);
}
