// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;

namespace Josupeit.Mail.Milter.Session;

/// <summary>
/// Drives one milter conversation: reads request frames, runs them through the protocol state machine,
/// and writes back whatever the filter answered.
/// </summary>
/// <remarks>
/// The state machine is what makes an out-of-order event an error rather than a surprise: each state
/// accepts only the events legal at that point and rejects the rest as a protocol violation. Reading and
/// writing happen on their own tasks either side of this loop, so a slow filter stalls the conversation
/// but never the transport.
/// </remarks>
/// <typeparam name="TContext">The filter this session runs. One instance is built per message.</typeparam>
public partial class Session<TContext> : ISession where TContext : IMilterContext
{
    private readonly ProtocolStateMachineConfiguration _protocolStateMachineConfiguration = new(
        TContext.ProtocolRequirements,
        TContext.ActionRequirements,
        TContext.DeliveryRequirements,
        TContext.NoReplyRequirements,
        TContext.ProtocolVersionRequirement,
        TContext.MacroRequirements);

    private readonly ChannelReader<RequestFrame> _requestFrameReader;
    private readonly IRequestPacketFactory _requestPacketFactory;
    private readonly IResponseFrameFactory _responseFrameFactory;
    private readonly ChannelWriter<ResponseFrame> _responseFrameWriter;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TContext> _contextFactory;

    private IProtocolState? _protocolState;
    private ContextAdapter? _contextAdapter;

    /// <summary>
    /// Creates a session over an already-connected pair of channels.
    /// </summary>
    /// <param name="requestFrameReader">The channel the frames from the MTA arrive on.</param>
    /// <param name="requestPacketFactory">Turns those frames into typed packets.</param>
    /// <param name="responseFrameFactory">Turns the answers back into frames.</param>
    /// <param name="responseFrameWriter">The channel the answers are published to.</param>
    /// <param name="timeout">
    /// How long to wait for the next frame, and for an answer to reach the link, before giving up on the
    /// connection. <see langword="null"/> means five minutes.
    /// </param>
    /// <param name="timeProvider">
    /// The clock behind that timeout, or <see langword="null"/> for the system clock. Tests substitute
    /// this to avoid waiting in real time.
    /// </param>
    /// <param name="contextFactory">
    /// Builds the filter, once per message. <see langword="null"/> falls back to
    /// <see cref="Activator"/>, which needs a public parameterless constructor.
    /// </param>
    public Session(
        ChannelReader<RequestFrame> requestFrameReader,
        IRequestPacketFactory requestPacketFactory,
        IResponseFrameFactory responseFrameFactory,
        ChannelWriter<ResponseFrame> responseFrameWriter,
        TimeSpan? timeout,
        TimeProvider? timeProvider,
        Func<TContext>? contextFactory)
    {
        _requestFrameReader = requestFrameReader;
        _requestPacketFactory = requestPacketFactory;
        _responseFrameFactory = responseFrameFactory;
        _responseFrameWriter = responseFrameWriter;

        _timeout = timeout ?? TimeSpan.FromMinutes(5);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _contextFactory = contextFactory ?? Activator.CreateInstance<TContext>;
    }

    /// <inheritdoc />
    public async Task Run(CancellationToken cancellationToken)
    {
        _protocolState = new AwaitingNegotiationState(this, _protocolStateMachineConfiguration);

        try
        {
            while (!_protocolState.IsFinal)
            {
                var requestFrame = await _requestFrameReader
                    .ReadAsync(cancellationToken)
                    .AsTask()
                    .WaitAsync(_timeout, _timeProvider, cancellationToken)
                    .ConfigureAwait(false);

                var requestPacket = _requestPacketFactory.CreateRequestPacket(requestFrame);

                try
                {
                    var responsePackets = await new SessionController(this)
                        .ProcessRequest(requestPacket, cancellationToken)
                        .ConfigureAwait(false);

                    await foreach (var responsePacket in responsePackets.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        var responseFrame = _responseFrameFactory.CreateResponseFrame(responsePacket);

                        await _responseFrameWriter
                            .WriteAsync(responseFrame, cancellationToken)
                            .AsTask()
                            .WaitAsync(_timeout, _timeProvider, cancellationToken)
                            .ConfigureAwait(false);

                        await responseFrame
                            .WaitAsync(_timeout, _timeProvider, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                finally
                {
                    switch (requestPacket)
                    {
                        case IAsyncDisposable asyncDisposable:
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                            break;

                        case IDisposable dispsable:
                            dispsable.Dispose();
                            break;
                    }
                }
            }
        }
        finally
        {
            // Nothing else completes the response channel. Without this the formatter's orphan drain
            // waits on a channel that never closes, so its task never finishes and one parked
            // continuation is left behind per connection.
            _responseFrameWriter.Complete();
        }
    }

    // The adapter is per message and the outcome is per connection, so the state that asks for one
    // carries the outcome in and the session only owns the lifetime.
    internal ContextAdapter EnsureContextAdapter(ProtocolNegotiationOutcome negotiation)
    {
        return _contextAdapter ??= new(() => _contextFactory(), negotiation);
    }

    internal ValueTask DisposeContextAdapter()
    {
        return Interlocked.Exchange(ref _contextAdapter, null)?.DisposeAsync() ?? new();
    }
}

partial class Session<TContext> : ISessionControlee
{
    IProtocolStateMachine ISessionControlee.ProtocolStateMachine => this;
}

partial class Session<TContext> : IProtocolStateMachine
{
    IProtocolState IStateMachine<IProtocolState>.State => _protocolState!;

    ProtocolStateMachineConfiguration IProtocolStateMachine.Configuration => _protocolStateMachineConfiguration;

    ValueTask IProtocolStateMachine.DisposeContextAdapter() => DisposeContextAdapter();

    ContextAdapter IProtocolStateMachine.EnsureContextAdapter(ProtocolNegotiationOutcome negotiation) => EnsureContextAdapter(negotiation);

    ValueTask IStateMachine<IProtocolState>.TransitionTo(IProtocolState state)
    {
        _protocolState = state;

        return new();
    }
}
