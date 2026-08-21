// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class DisconnectedState : ProtocolState
{
    public override bool IsFinal => true;
}
