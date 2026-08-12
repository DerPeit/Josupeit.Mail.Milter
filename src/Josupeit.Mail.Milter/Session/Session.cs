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

public partial class Session<TContext> : ISession where TContext : IMilterContext
{
    private readonly ProtocolStateMachineConfiguration _protocolStateMachineConfiguration = new(TContext.ProtocolRequirements, TContext.ActionRequirements);

    private readonly ChannelReader<RequestFrame> _requestFrameReader;
    private readonly IRequestPacketFactory _requestPacketFactory;
    private readonly IResponseFrameFactory _responseFrameFactory;
    private readonly ChannelWriter<ResponseFrame> _responseFrameWriter;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TContext> _contextFactory;

    private IProtocolState? _protocolState;
    private ContextAdapter? _contextAdapter;

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

    internal ContextAdapter EnsureContextAdapter()
    {
        return _contextAdapter ??= new(() => _contextFactory());
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

    ContextAdapter IProtocolStateMachine.EnsureContextAdapter() => EnsureContextAdapter();

    ValueTask IStateMachine<IProtocolState>.TransitionTo(IProtocolState state)
    {
        _protocolState = state;

        return new();
    }
}
