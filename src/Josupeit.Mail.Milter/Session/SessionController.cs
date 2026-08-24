// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session;

internal sealed class SessionController(ISessionControlee controlee) : RequestPacketVisitor
{
    private ValueTask<IAsyncEnumerable<ResponsePacket>> _responses;
    private CancellationToken _cancellationToken;

    public ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessRequest(RequestPacket request, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        // Nothing having handled the packet leaves _responses at its default, and awaiting that hands
        // back null for the session to enumerate - a NullReferenceException from inside the read loop,
        // naming nothing. This is the same answer the response encoder gives to a packet it cannot
        // place, and it is reachable the same two ways: a packet type declared outside this assembly,
        // or a visit method that returned false where it should not have.
        if (!VisitRequestPacket(request))
            throw new ArgumentException($"Unable to process the supplied {nameof(RequestPacket)}. No visit method handled a {request.GetType().Name}.", nameof(request));

        return _responses;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitAbortRequestPacket(AbortRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessAbortion(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitBodyChunkRequestPacket(BodyChunkRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessBodyChunk(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitConnectRequestPacket(ConnectRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessConnectionAttempt(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitDefineMacroRequestPacket(DefineMacroRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessMacroDefinition(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitEndOfBodyRequestPacket(EndOfBodyRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessEndOfBody(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitEndOfHeadersRequestPacket(EndOfHeadersRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessEndOfHeaders(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitHeaderRequestPacket(HeaderRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessHeader(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitHeloRequestPacket(HeloRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessClientHello(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitMailFromRequestPacket(SenderRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessSender(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitMessageDataRequestPacket(MessageDataRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessMessageDataStart(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitOptionsNegotiationRequestPacket(OptionNegotiationRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessOptionNegotiationAttempt(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitQuitRequestPacket(QuitRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessQuitting(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitQuitForNewConnectionRequestPacket(QuitForNewConnectionRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessQuittingForNewConnection(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitRcptToRequestPacket(RecipientRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessRecipient(requestPacket, _cancellationToken);

        return true;
    }

    [SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "The controller is created per request, so the field is written once and awaited once, by the caller of ProcessRequest.")]
    protected internal override bool VisitUnknownCommandRequestPacket(UnknownCommandRequestPacket requestPacket)
    {
        _responses = controlee.ProtocolStateMachine.State.ProcessUnknownCommand(requestPacket, _cancellationToken);

        return true;
    }
}
