using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public class ResponseFormatter
{
    private readonly ChannelReader<ResponseFrame> _inputReader;
    private readonly ILinkWriter _linkWriter;

    private CancellationTokenSource? _stopper;
    private Task? _runner;

    public ResponseFormatter(ChannelReader<ResponseFrame> inputReader, ILinkWriter linkWriter)
    {
        _inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        _linkWriter = linkWriter ?? throw new ArgumentNullException(nameof(linkWriter));
    }

    public void Start()
    {
        if (_runner != null)
            throw new InvalidOperationException($"{nameof(ResponseFormatter)} cannot be started. It is already running.");

        _stopper = new();
        _runner = Task.Run(ProcessFramesCore, _stopper.Token);
    }

    public async Task StopAsync()
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
                        if (frame.Memory == null)
                        {
                            await _linkWriter.WriteUInt32(sizeof(ResponseCode), cancellationToken).ConfigureAwait(false);
                            await _linkWriter.WriteResponseCode(frame.ResponseCode, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            var data = frame.Memory.Memory[frame.DataRange];
                            var frameLength = (uint)(sizeof(ResponseCode) + data.Length);

                            await _linkWriter.WriteUInt32(frameLength, cancellationToken).ConfigureAwait(false);
                            await _linkWriter.WriteResponseCode(frame.ResponseCode, cancellationToken).ConfigureAwait(false);
                            await _linkWriter.WriteData(data.Span, cancellationToken).ConfigureAwait(false);
                        }

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