using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal interface IProtocolStateMachine : IStateMachine<IProtocolState>
{
    ContextAdapter EnsureContextAdapter();

    ValueTask DisposeContextAdapter();
}