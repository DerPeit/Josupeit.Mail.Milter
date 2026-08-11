using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter.Session.ProtocolEnforcement;

internal sealed class AwaitingNegotiationState(IProtocolStateMachine stateMachine, ProtocolStateMachineConfiguration config) : ProtocolState
{
    private static readonly Version _implementedProtocolVersion = new(2, 0);

    public override IAsyncEnumerable<ResponsePacket> ProcessOptionNegotiationAttempt(OptionNegotiationRequestPacket request, CancellationToken cancellationToken)
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

        return Enumerate();

        async IAsyncEnumerable<ResponsePacket> Enumerate()
        {
            try
            {
                yield return response;
            }
            finally
            {
                await TransitionToNextState().ConfigureAwait(false);
            }
        }
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

    private ValueTask TransitionToNextState()
    {
        ProtocolState nextState = config.IsAwaitingConnection
            ? new ReplayRecordingNegotiatedState(stateMachine)
            : new NegotiatedState(stateMachine);

        return stateMachine.TransitionTo(nextState);
    }
}