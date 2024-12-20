using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public class RequestParser : IRequestParser
{
    private readonly ILinkReader _linkReader;
    private readonly ChannelWriter<RequestFrame> _outputWriter;

    private CancellationTokenSource? _stopper;
    private Task? _runner;

    public RequestParser(ILinkReader linkReader, ChannelWriter<RequestFrame> outputWriter)
    {
        _linkReader = linkReader ?? throw new ArgumentNullException(nameof(linkReader));
        _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
    }

    public void Start()
    {
        if (_runner != null)
            throw new InvalidOperationException($"{nameof(RequestParser)} cannot be started. It is already running.");

        _stopper = new();

        _runner = Task.Factory.StartNew(
                ProcessFramesCore,
                _stopper.Token,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default)
            .Unwrap();
    }

    public async ValueTask StopAsync()
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

    private Task ProcessFramesCore() => ProcessFrames(_stopper!.Token);
}