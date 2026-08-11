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
    private readonly Socket _socket;
    private readonly MilterOptions _options;
    private readonly TimeSpan? _timeout;
    private readonly TimeProvider? _timeProvider;
    private readonly Func<TContext>? _contextFactory;

    private CancellationTokenSource? _stopper;
    private Task? _runner;
    private AsyncMonitor? _monitor;
    private int _activeSessions;

    public Milter(Socket socket, MilterOptions? options = null)
        : this(
            socket ?? throw new ArgumentNullException(nameof(socket)),
            options,
            timeout: null,
            timeProvider: null,
            contextFactory: null)
    {
    }

    internal Milter(
        Socket socket,
        MilterOptions? options = null,
        TimeSpan? timeout = null,
        TimeProvider? timeProvider = null,
        Func<TContext>? contextFactory = null)
    {
        _socket = socket;
        _options = options ?? new MilterOptions();
        _timeout = timeout;
        _timeProvider = timeProvider;
        _contextFactory = contextFactory;
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

    public async Task Stop()
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
            // Deliberately unguarded for now. A transient socket error here, EMFILE under connection
            // pressure for instance, faults this loop and stops the listener while the process stays up
            // and healthy-looking. Handling it means deciding what to report and whether to continue,
            // which waits on the diagnostics decision noted in RunSession.
            var connection = await _socket.AcceptAsync(cancellationToken).ConfigureAwait(false);

            bool hasCapacity;

            using (await _monitor!.EnterAsync(CancellationToken.None).ConfigureAwait(false))
            {
                hasCapacity = _activeSessions < _options.MaxConcurrentSessions;

                if (hasCapacity)
                    _activeSessions++;
            }

            // Refuse rather than queue. A peer we have no capacity for should find that out immediately
            // instead of holding a socket open while we are saturated.
            if (!hasCapacity)
            {
                connection.Dispose();

                continue;
            }

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
                        var linkReader = new LinkReader(streamReader, _options.MaxFrameLength);
                        var linkWriter = new LinkWriter(streamWriter);

                        var requestParser = new RequestParser(linkReader, requestFrameChannel.Writer, _options.MaxFrameLength);
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
                    // Every way a session can end badly arrives here indistinguishably: protocol
                    // violations, negotiation failures, exceptions thrown by the filter's callbacks, a
                    // frame we refused to parse, and a peer that simply went away. Dropping the
                    // connection is the right outcome for all of them, so the only thing missing is a
                    // way to tell them apart from the outside. That is a public API decision - logger,
                    // event source, or an exception callback - and it has deliberately not been made
                    // yet, which is why nothing is reported here at all.
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