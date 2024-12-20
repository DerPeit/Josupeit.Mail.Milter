using Josupeit.Mail.Milter.Session.ProtocolEnforcement;

namespace Josupeit.Mail.Milter.Session;

internal interface ISessionControlee
{
    IProtocolStateMachine ProtocolStateMachine { get; }
}