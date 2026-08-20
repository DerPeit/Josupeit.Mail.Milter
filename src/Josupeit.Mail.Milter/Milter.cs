// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

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

/// <summary>
/// Hosts a filter: accepts connections from the MTA and runs one protocol session per connection.
/// </summary>
/// <remarks>
/// <para>
/// The socket is supplied already bound and listening, which keeps the choice of endpoint - TCP or a Unix
/// domain socket - out of here entirely. <see cref="Start"/> returns as soon as the accept loop is
/// running; <see cref="Stop"/> waits for the sessions still in flight, so a message being filtered at
/// that moment is not cut off half-way.
/// </para>
/// <para>
/// A session that ends badly - a protocol violation, a frame that was refused, an exception out of a
/// callback, or a peer that simply went away - drops that one connection and leaves the rest running.
/// Nothing is reported about it: there is no logging surface yet, deliberately, because what that should
/// look like is still an open question.
/// </para>
/// </remarks>
/// <typeparam name="TContext">
/// The filter. One instance is built per message, either through the factory passed to the constructor
/// or through <see cref="System.Activator"/>, which needs a public parameterless constructor.
/// </typeparam>
public class Milter<TContext> where TContext : IMilterContext
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

    // Four overloads and not an optional parameter among them, which is uglier than two and correct
    // where two were not. Both of the old pair ended in an optional MilterOptions, so `new Milter<T>(
    // socket, null)` could not be resolved at all, and giving either one a further optional parameter
    // later would have silently rebound call sites that already compiled. RS0026 says so, and it is
    // right.

    /// <summary>
    /// Creates a milter that builds each context through <see cref="System.Activator"/>, with the
    /// default limits.
    /// </summary>
    /// <param name="socket">A socket already bound and listening. The milter accepts on it but does not own
    /// it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="socket"/> is <see langword="null"/>.</exception>
    public Milter(Socket socket)
        : this(
            socket ?? throw new ArgumentNullException(nameof(socket)),
            contextFactory: null,
            options: null,
            timeout: null,
            timeProvider: null)
    {
    }

    /// <summary>
    /// Creates a milter that builds each context through <see cref="System.Activator"/>.
    /// </summary>
    /// <param name="socket">A socket already bound and listening. The milter accepts on it but does not own
    /// it.</param>
    /// <param name="options">Limits on frame size and session count.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="socket"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public Milter(Socket socket, MilterOptions options)
        : this(
            socket ?? throw new ArgumentNullException(nameof(socket)),
            contextFactory: null,
            options ?? throw new ArgumentNullException(nameof(options)),
            timeout: null,
            timeProvider: null)
    {
    }

    /// <summary>
    /// Creates a milter that builds each context through a factory, with the default limits. Use this
    /// when the filter has dependencies.
    /// </summary>
    /// <param name="socket">A socket already bound and listening. The milter accepts on it but does not own
    /// it.</param>
    /// <param name="contextFactory">
    /// Called once per message. It may be called from several sessions at once, so it has to be safe to
    /// call concurrently - the contexts it returns need not be.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="socket"/> or <paramref name="contextFactory"/> is <see langword="null"/>.
    /// </exception>
    public Milter(Socket socket, Func<TContext> contextFactory)
        : this(
            socket ?? throw new ArgumentNullException(nameof(socket)),
            contextFactory ?? throw new ArgumentNullException(nameof(contextFactory)),
            options: null,
            timeout: null,
            timeProvider: null)
    {
    }

    /// <summary>
    /// Creates a milter that builds each context through a factory. Use this when the filter has
    /// dependencies.
    /// </summary>
    /// <param name="socket">A socket already bound and listening. The milter accepts on it but does not own
    /// it.</param>
    /// <param name="contextFactory">
    /// Called once per message. It may be called from several sessions at once, so it has to be safe to
    /// call concurrently - the contexts it returns need not be.
    /// </param>
    /// <param name="options">Limits on frame size and session count.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="socket"/>, <paramref name="contextFactory"/> or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    public Milter(Socket socket, Func<TContext> contextFactory, MilterOptions options)
        : this(
            socket ?? throw new ArgumentNullException(nameof(socket)),
            contextFactory ?? throw new ArgumentNullException(nameof(contextFactory)),
            options ?? throw new ArgumentNullException(nameof(options)),
            timeout: null,
            timeProvider: null)
    {
    }

    internal Milter(
        Socket socket,
        Func<TContext>? contextFactory,
        MilterOptions? options,
        TimeSpan? timeout,
        TimeProvider? timeProvider)
    {
        _socket = socket;
        _options = options ?? new MilterOptions();
        _timeout = timeout;
        _timeProvider = timeProvider;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Starts accepting connections and returns immediately.
    /// </summary>
    /// <exception cref="InvalidOperationException">The milter is already running.</exception>
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

    /// <summary>
    /// Stops accepting connections and waits for the sessions still running to finish.
    /// </summary>
    /// <remarks>
    /// The wait is what makes shutdown graceful: a message under inspection is filtered to its end and
    /// answered rather than abandoned. It is unbounded, so a filter that never returns from a callback
    /// will hold this open - bound it from the caller if that is a risk. The milter can be started again
    /// afterwards.
    /// </remarks>
    /// <returns>A task that completes once every session has ended.</returns>
    /// <exception cref="InvalidOperationException">The milter is not running.</exception>
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

        await WaitForSessionsToEnd().ConfigureAwait(false);

        _stopper.Dispose();
        _stopper = null;
        _runner = null;
        _monitor = null;
    }

    internal async Task WaitForSessionsToEnd()
    {
        using (await _monitor!.EnterAsync().ConfigureAwait(false))
            while (_activeSessions > 0)
                await _monitor.WaitAsync().ConfigureAwait(false);
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

    // The six pieces of the per-connection pipeline, each behind a method a derived milter can replace.
    // Without these the wire layer is public and non-sealed for nothing: a consumer can subclass a packet
    // factory or a link reader perfectly well, and has nowhere to hand the result to, because this class
    // used to build them all with new. Constructing a Session by hand is the alternative, and it means
    // rebuilding the accept loop, the pipes and both channels as well.
    //
    // Called once per connection rather than once per milter, so an override may return a fresh instance
    // each time or a shared one - whichever it is has to be safe for the connections that share it.

    /// <summary>
    /// Creates the reader that turns the connection's bytes into frames.
    /// </summary>
    /// <param name="reader">The pipe over the connection's stream.</param>
    /// <param name="maxFrameLength">The largest frame to accept, from the options this milter was given.</param>
    /// <returns>The reader for one connection.</returns>
    protected virtual ILinkReader CreateLinkReader(PipeReader reader, uint maxFrameLength)
    {
        return new LinkReader(reader, maxFrameLength);
    }

    /// <summary>
    /// Creates the writer that puts frames back on the connection.
    /// </summary>
    /// <param name="writer">The pipe over the connection's stream.</param>
    /// <returns>The writer for one connection.</returns>
    protected virtual ILinkWriter CreateLinkWriter(PipeWriter writer)
    {
        return new LinkWriter(writer);
    }

    /// <summary>
    /// Creates the worker that reads frames and puts them on the request channel.
    /// </summary>
    /// <param name="linkReader">The reader to take frames from.</param>
    /// <param name="frameWriter">The channel the session reads its requests from.</param>
    /// <param name="maxFrameLength">The largest frame to accept, from the options this milter was given.</param>
    /// <returns>The parser for one connection.</returns>
    protected virtual IRequestParser CreateRequestParser(ILinkReader linkReader, ChannelWriter<RequestFrame> frameWriter, uint maxFrameLength)
    {
        return new RequestParser(linkReader, frameWriter, maxFrameLength);
    }

    /// <summary>
    /// Creates the worker that takes the session's responses and writes them out.
    /// </summary>
    /// <param name="frameReader">The channel the session writes its responses to.</param>
    /// <param name="linkWriter">The writer to hand frames to.</param>
    /// <returns>The formatter for one connection.</returns>
    protected virtual IResponseFormatter CreateResponseFormatter(ChannelReader<ResponseFrame> frameReader, ILinkWriter linkWriter)
    {
        return new ResponseFormatter(frameReader, linkWriter);
    }

    /// <summary>
    /// Creates the factory that turns request frames into typed packets.
    /// </summary>
    /// <returns>The factory for one connection.</returns>
    protected virtual IRequestPacketFactory CreateRequestPacketFactory()
    {
        return new RequestPacketFactory();
    }

    /// <summary>
    /// Creates the factory that turns response packets back into frames.
    /// </summary>
    /// <returns>The factory for one connection.</returns>
    protected virtual IResponseFrameFactory CreateResponseFrameFactory()
    {
        return new ResponseFrameFactory();
    }

    /// <summary>
    /// Creates the session that drives one connection's protocol state machine.
    /// </summary>
    /// <remarks>
    /// The last of the pieces, and the one the other six are handed to. The read timeout and the time
    /// source are deliberately not parameters: they exist so this library's own tests can drive a session
    /// without waiting, and an override that wants different ones can construct a
    /// <see cref="Session.Session{TContext}"/> with them directly.
    /// </remarks>
    /// <param name="requestFrameReader">The channel the parser publishes frames on.</param>
    /// <param name="requestPacketFactory">The factory turning those frames into packets.</param>
    /// <param name="responseFrameFactory">The factory turning answers back into frames.</param>
    /// <param name="responseFrameWriter">The channel the formatter takes answers from.</param>
    /// <param name="contextFactory">
    /// How to build the filter for each message, or <see langword="null"/> to build it through
    /// <see cref="System.Activator"/>.
    /// </param>
    /// <returns>The session for one connection.</returns>
    protected virtual ISession CreateSession(
        ChannelReader<RequestFrame> requestFrameReader,
        IRequestPacketFactory requestPacketFactory,
        IResponseFrameFactory responseFrameFactory,
        ChannelWriter<ResponseFrame> responseFrameWriter,
        Func<TContext>? contextFactory)
    {
        return new Session<TContext>(
            requestFrameReader,
            requestPacketFactory,
            responseFrameFactory,
            responseFrameWriter,
            contextFactory, _timeout, _timeProvider);
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
                        var linkReader = CreateLinkReader(streamReader, _options.MaxFrameLength);
                        var linkWriter = CreateLinkWriter(streamWriter);

                        var requestParser = CreateRequestParser(linkReader, requestFrameChannel.Writer, _options.MaxFrameLength);
                        var responseFormatter = CreateResponseFormatter(responseFrameChannel.Reader, linkWriter);

                        var requestPacketFactory = CreateRequestPacketFactory();
                        var responseFrameFactory = CreateResponseFrameFactory();

                        var session = CreateSession(
                            requestFrameChannel.Reader,
                            requestPacketFactory,
                            responseFrameFactory,
                            responseFrameChannel.Writer,
                            _contextFactory);

                        requestParser.Start();
                        responseFormatter.Start();

                        try
                        {
                            await session.Run(cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            await requestParser.Stop().ConfigureAwait(false);
                            await responseFormatter.Stop().ConfigureAwait(false);
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
