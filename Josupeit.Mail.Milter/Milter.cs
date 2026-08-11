using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session;
using Nito.AsyncEx;

namespace Josupeit.Mail.Milter;

public sealed class Milter<TContext> where TContext : IMilterContext
{
    /// <summary>
    /// The default upper bound for the length prefix of a single request frame, in bytes.
    /// </summary>
    /// <remarks>
    /// The length prefix is attacker-controlled, so it must be bounded before it is used to size a read
    /// or an allocation. This default sits comfortably above everything Postfix generates: its body
    /// chunks are capped at MILTER_CHUNK_SIZE (65535) and its default header_size_limit is 102400.
    /// Lower it if the MTA is known to be more constrained.
    /// </remarks>
    public static readonly uint DefaultMaxFrameLength = 1024 * 1024;

    private readonly Socket _socket;
    private readonly TimeSpan? _timeout;
    private readonly TimeProvider? _timeProvider;
    private readonly Func<TContext>? _contextFactory;
    private readonly uint _maxFrameLength;

    private CancellationTokenSource? _stopper;
    private Task? _runner;
    private AsyncMonitor? _monitor;
    private int _activeSessions;

    public Milter(Socket socket)
        : this(
            socket ?? throw new ArgumentNullException(nameof(socket)),
            timeout: null,
            timeProvider: null,
            contextFactory: null,
            maxFrameLength: null)
    {
    }

    public Milter(Socket socket, uint maxFrameLength)
        : this(
            socket ?? throw new ArgumentNullException(nameof(socket)),
            timeout: null,
            timeProvider: null,
            contextFactory: null,
            maxFrameLength)
    {
    }

    internal Milter(
        Socket socket,
        TimeSpan? timeout = null,
        TimeProvider? timeProvider = null,
        Func<TContext>? contextFactory = null,
        uint? maxFrameLength = null)
    {
        if (maxFrameLength < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFrameLength), maxFrameLength, message: null);

        _socket = socket;
        _timeout = timeout;
        _timeProvider = timeProvider;
        _contextFactory = contextFactory;
        _maxFrameLength = maxFrameLength ?? DefaultMaxFrameLength;
    }

    public void Start()
    {
        if (_runner != null)
            throw new InvalidOperationException("Unable to start Milter. Milter is already running.");

        _stopper = new();
        _monitor = new();

        _runner = Task.Factory.StartNew(
                AcceptConnections,
                _stopper.Token,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap();
    }

    public async ValueTask Stop()
    {
        if (_runner == null)
            throw new InvalidOperationException("Unable to stop Milter. Milter is not running.");

        await _stopper!.CancelAsync().ConfigureAwait(false);

        if (!_runner.IsCompleted)
        {
            try
            {
                await _runner.ConfigureAwait(false);
            }
            catch
            {
                // Deliberately discarded.
            }
        }

        using (await _monitor!.EnterAsync().ConfigureAwait(false))
            while (_activeSessions > 0)
                await _monitor.WaitAsync().ConfigureAwait(false);

        _stopper.Dispose();
        _stopper = null;
        _runner = null;
        _monitor = null;
    }

    private async Task AcceptConnections()
    {
        var cancellationToken = _stopper!.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            var connection = await _socket.AcceptAsync(cancellationToken).ConfigureAwait(false);

            using (await _monitor!.EnterAsync(CancellationToken.None).ConfigureAwait(false))
                _activeSessions++;

            _ = Task.Factory.StartNew(
                RunSession,
                connection,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    private async Task RunSession(object? socket)
    {
        try
        {
            var cancellationToken = _stopper!.Token;
            var stream = new NetworkStream((Socket)socket!, ownsSocket: true);

            await using (stream.ConfigureAwait(false))
            {
                try
                {
                    var requestFrameChannel = Channel.CreateBounded<RequestFrame>(new BoundedChannelOptions(capacity: 1) { SingleReader = true, SingleWriter = true });
                    var responseFrameChannel = Channel.CreateBounded<ResponseFrame>(new BoundedChannelOptions(capacity: 1) { SingleReader = true });

                    var streamReader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
                    var streamWriter = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));

                    try
                    {
                        var linkReader = new LinkReader(streamReader, _maxFrameLength);
                        var linkWriter = new LinkWriter(streamWriter);

                        var requestParser = new RequestParser(linkReader, requestFrameChannel.Writer, _maxFrameLength);
                        var responseFormatter = new ResponseFormatter(responseFrameChannel.Reader, linkWriter);

                        var requestPacketFactory = new RequestPacketFactory();
                        var responseFrameFactory = new ResponseFrameFactory();

                        var session = new Session<TContext>(
                            requestFrameChannel.Reader,
                            requestPacketFactory,
                            responseFrameFactory,
                            responseFrameChannel.Writer,
                            _timeout,
                            _timeProvider,
                            _contextFactory);

                        requestParser.Start();
                        responseFormatter.Start();

                        try
                        {
                            await session.Run(cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            await requestParser.StopAsync().ConfigureAwait(false);
                            await responseFormatter.StopAsync().ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        await streamReader.CompleteAsync().ConfigureAwait(false);
                        await streamWriter.CompleteAsync().ConfigureAwait(false);
                    }
                }
                catch
                {
                    // TODO: Add logging
                }
            }
        }
        finally
        {
            using (await _monitor!.EnterAsync(CancellationToken.None).ConfigureAwait(false))
                if (--_activeSessions == 0)
                    _monitor.PulseAll();
        }
    }
}