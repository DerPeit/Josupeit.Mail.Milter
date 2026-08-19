// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

// The state a session sits in once negotiation is done and no message has started.
//
// Two callers need it and must agree: AwaitingNegotiationState after the OPTNEG reply, and every state
// handling SMFIC_QUIT_NC, which ends the SMTP connection without ending the milter one. Picking the
// wrong one is not a visible mistake - it surfaces only as a protocol violation on the next
// connection's SMFIC_CONNECT - so the choice lives here rather than being written out twice.
internal static class NegotiatedStateFactory
{
    // A filter that asked for the connection event needs its connect and macro packets recorded, because
    // an abort resets the context and the next message on the same connection has to be told about the
    // connection again.
    //
    // The recording state this hands back is always a fresh one, and for SMFIC_QUIT_NC that is the whole
    // point. An abort resets to before HELO on the same connection, so the recorded packets stay true
    // and ReplayRecordingNegotiatedState deliberately keeps them. QUIT_NC ends the connection: a
    // different client follows, and replaying the previous one's SMFIC_CONNECT would report the wrong
    // peer to the filter, silently, for every message that client sends.
    public static ProtocolState Create(IProtocolStateMachine stateMachine, ProtocolStateMachineConfiguration configuration)
    {
        return configuration.IsAwaitingConnection
            ? new ReplayRecordingNegotiatedState(stateMachine)
            : new NegotiatedState(stateMachine);
    }
}
