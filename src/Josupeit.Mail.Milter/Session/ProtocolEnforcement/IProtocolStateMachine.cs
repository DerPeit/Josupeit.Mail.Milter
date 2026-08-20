// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal interface IProtocolStateMachine : IStateMachine<IProtocolState>
{
    // The filter's own declarations, fixed when the session is constructed and true of every state the
    // machine will ever reach. What the negotiation settled on is not an invariant of the machine, so it
    // lives in the states that exist after it rather than here.
    ProtocolStateMachineConfiguration Configuration { get; }

    ContextAdapter EnsureContextAdapter(ProtocolNegotiationOutcome negotiation);

    ValueTask DisposeContextAdapter();
}
