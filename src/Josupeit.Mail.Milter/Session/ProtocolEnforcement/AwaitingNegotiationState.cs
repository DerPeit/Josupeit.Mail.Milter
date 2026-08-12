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
    private static readonly Version _implementedProtocolVersion = new(2, 0);

    public override ValueTask<IAsyncEnumerable<ResponsePacket>> ProcessOptionNegotiationAttempt(OptionNegotiationRequestPacket request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        ThrowIfUnsupportedVersion(request.MtaProtocolVersion);

        var actionRequirements = config.ToActionDiscriminator();
        var contentBypass = config.ToContentBypassDiscriminator();

        ThrowIfUnsupportedProtocolRequirements(request.MtaSupportedContentBypass, contentBypass);
        ThrowIfUnsupportedActionRequirements(request.MtaSupportedActions, actionRequirements);

        var response = new OptionNegotiationResponsePacket
        {
            MilterProtocolVersion = _implementedProtocolVersion,
            MilterRequiredActions = actionRequirements,
            MilterUndesiredProtocolContent = contentBypass,
        };

        return new(AsyncEnumerableEx.Return<ResponsePacket>(response).Finally(TransitionToNextState));
    }

    private static void ThrowIfUnsupportedVersion(Version supportedVersion)
    {
        if (supportedVersion < _implementedProtocolVersion)
            throw new NotSupportedException(
                $"The MTA implements Milter protocol version {supportedVersion}. However, this Milter implements " +
                $"protocol version {_implementedProtocolVersion}, which is unsupported. Upgrade the MTA and retry.");
    }

    private static void ThrowIfUnsupportedProtocolRequirements(ContentBypassDiscriminator supportedFlags, ContentBypassDiscriminator requiredFlags)
    {
        if (supportedFlags.HasFlag(requiredFlags))
            return;

        throw new NotSupportedException(
            $"The MTA only supports the Milter protocol events {supportedFlags:F}. However, this Milter requires " +
            $"events {requiredFlags:F}, which is unsupported. Upgrade the MTA and retry.");
    }

    private static void ThrowIfUnsupportedActionRequirements(ActionDiscriminator supportedFlags, ActionDiscriminator requiredFlags)
    {
        if (supportedFlags.HasFlag(requiredFlags))
            return;

        throw new NotSupportedException(
            $"The MTA only supports the Milter actions {supportedFlags:F}. However, this Milter requires " +
            $"actions {requiredFlags:F}, which is unsupported. Upgrade the MTA and retry.");
    }

    private async Task TransitionToNextState()
    {
        ProtocolState nextState = config.IsAwaitingConnection
            ? new ReplayRecordingNegotiatedState(stateMachine)
            : new NegotiatedState(stateMachine);

        await stateMachine.TransitionTo(nextState).ConfigureAwait(false);
    }
}