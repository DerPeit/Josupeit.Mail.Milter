// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Session.ProtocolEnforcement;

namespace Josupeit.Mail.Milter.Session;

internal interface ISessionControlee
{
    IProtocolStateMachine ProtocolStateMachine { get; }
}
