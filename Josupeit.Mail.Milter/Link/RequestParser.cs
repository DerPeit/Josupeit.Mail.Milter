using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public class RequestParser : IRequestParser
{
    private readonly ILinkReader _linkReader;
    private readonly ChannelWriter<RequestFrame> _outputWriter;
    private readonly uint _maxFrameLength;

    private CancellationTokenSource? _stopper;
    private Task? _runner;

    public RequestParser(ILinkReader linkReader, ChannelWriter<RequestFrame> outputWriter, uint maxFrameLength)
    {
        if (maxFrameLength < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFrameLength), maxFrameLength, message: null);

        _linkReader = linkReader ?? throw new ArgumentNullException(nameof(linkReader));
        _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        _maxFrameLength = maxFrameLength;
    }

    public void Start()
    {
        if (_runner != null)
            throw new InvalidOperationException($"{nameof(RequestParser)} cannot be started. It is already running.");

        _stopper = new();
        _runner = Task.Run(ProcessFramesCore, _stopper.Token);
    }

    public async Task StopAsync()
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

    protected virtual async Task ProcessFrames(CancellationToken cancellationToken)
    {
        Exception? error = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var length = await _linkReader.ReadUInt32(cancellationToken).ConfigureAwait(false);

                // The length covers the command byte, so it must be at least 1. Guarding here is what
                // keeps "length - 1" from underflowing to uint.MaxValue and turning a zero-length frame
                // into a 4 GiB read, and what keeps an announced length from sizing an unbounded rent.
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