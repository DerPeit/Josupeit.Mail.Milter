namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class DisconnectedState : ProtocolState
{
    public override bool IsFinal => true;
}