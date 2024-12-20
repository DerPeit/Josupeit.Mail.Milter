using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session;

internal interface IStateMachine<TState>
{
    TState State { get; }

    ValueTask TransitionTo(TState state);
}