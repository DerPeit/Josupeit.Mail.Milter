// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class AwaitingNegotiationState(IProtocolStateMachine stateMachine, ProtocolStateMachineConfiguration config) : ProtocolState
{
    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessOptionNegotiationAttempt(OptionNegotiationRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var negotiatedVersion = ProtocolVersionCapabilities.Clamp(request.MtaProtocolVersion);

        var actionRequirements = config.ToActionDiscriminator() & ProtocolVersionCapabilities.ActionsFor(negotiatedVersion);
        var contentBypass = config.ToContentBypassDiscriminator() & ProtocolVersionCapabilities.ContentBypassFor(negotiatedVersion);

        ThrowIfUnsupportedActionRequirements(request.MtaSupportedActions, actionRequirements);

        // Actions fail the session; events quietly degrade. A modification the MTA will not grant is
        // dropped on arrival with nothing anywhere in this library to notice, so the message goes out
        // unmodified and the operator hears about it from a user - worth failing the first connection
        // over. An event the MTA will not skip merely arrives anyway, and the callback that was never
        // overridden answers Continue, which is what happened before any of this was negotiated.
        contentBypass &= request.MtaSupportedContentBypass;

        var response = new OptionNegotiationResponsePacket
        {
            MilterProtocolVersion = negotiatedVersion,
            MilterRequiredActions = actionRequirements,
            MilterUndesiredProtocolContent = contentBypass,
        };

        stateMachine.CompleteNegotiation(new ProtocolNegotiationOutcome(negotiatedVersion, actionRequirements, contentBypass));

        return new(AsyncEnumerableEx.Return<ResponsePacket>(response).Finally(TransitionToNextState));
    }

    private static void ThrowIfUnsupportedActionRequirements(ActionDiscriminator supportedFlags, ActionDiscriminator requiredFlags)
    {
        if (supportedFlags.HasFlag(requiredFlags))
            return;

        throw new NotSupportedException(
            $"The MTA only supports the Milter actions {supportedFlags:F}. However, this Milter requires " +
            $"actions {requiredFlags:F}, which is unsupported. The requirement is derived from every action " +
            "the filter's code mentions, so declaring ActionRequirements by hand narrows it.");
    }

    private async Task TransitionToNextState()
    {
        await stateMachine.TransitionTo(NegotiatedStateFactory.Create(stateMachine, config)).ConfigureAwait(false);
    }
}