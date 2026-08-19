// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal interface IProtocolStateMachine : IStateMachine<IProtocolState>
{
    ProtocolStateMachineConfiguration Configuration { get; }

    ProtocolNegotiationOutcome Negotiation { get; }

    void CompleteNegotiation(ProtocolNegotiationOutcome outcome);

    ContextAdapter EnsureContextAdapter();

    ValueTask DisposeContextAdapter();
}