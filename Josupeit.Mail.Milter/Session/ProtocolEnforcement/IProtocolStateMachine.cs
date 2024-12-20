using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal interface IProtocolStateMachine : IStateMachine<IProtocolState>
{
    ProtocolStateMachineConfiguration Configuration { get; }

    ContextAdapter EnsureContextAdapter();

    ValueTask DisposeContextAdapter();
}