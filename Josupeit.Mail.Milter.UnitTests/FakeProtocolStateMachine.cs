using System;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Session;
using Josupeit.Mail.Milter.Session.ProtocolEnforcement;

namespace Josupeit.Mail.Milter.UnitTests;

/// <summary>
/// Drives a single <see cref="IProtocolState"/> without a socket, a session or an MTA, and records what
/// the state asked the machine to do.
/// </summary>
/// <remarks>
/// Hand-written rather than substituted because <see cref="IProtocolStateMachine"/> is internal to the
/// library, which a mocking proxy cannot see without widening the library's internals to it.
/// </remarks>
internal sealed class FakeProtocolStateMachine : IProtocolStateMachine
{
    private readonly Func<IMilterCallbacks> _contextFactory;

    private ContextAdapter? _contextAdapter;

    public FakeProtocolStateMachine(Func<IMilterCallbacks>? contextFactory = null)
    {
        _contextFactory = contextFactory ?? (static () => new TestMilterContext());
    }

    public IProtocolState? State { get; private set; }

    public int ContextAdapterDisposalCount { get; private set; }

    public ProtocolStateMachineConfiguration Configuration { get; init; }
        = new(ProtocolRequirements.All, ActionRequirements.None);

    IProtocolState IStateMachine<IProtocolState>.State => State!;

    public ContextAdapter EnsureContextAdapter()
    {
        return _contextAdapter ??= new ContextAdapter(_contextFactory);
    }

    public ValueTask DisposeContextAdapter()
    {
        ContextAdapterDisposalCount++;

        return Interlocked.Exchange(ref _contextAdapter, null)?.DisposeAsync() ?? new ValueTask();
    }

    public ValueTask TransitionTo(IProtocolState state)
    {
        State = state;

        return new ValueTask();
    }
}
