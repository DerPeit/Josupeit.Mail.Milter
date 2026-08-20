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

        ThrowIfBelowFilterRequirement(negotiatedVersion, config.ProtocolVersionRequirement);

        var requestedActions = config.ToActionDiscriminator();
        var grantedActions = request.MtaSupportedActions & ProtocolVersionCapabilities.ActionsFor(negotiatedVersion);

        // Checked against what the filter asked for, not against what survived the version mask. Masking
        // first and checking the remainder is how a version 6 modification against an older MTA became
        // silence: the flag was gone before anything could notice it was missing, and no runtime check
        // anywhere in this library reads the answer, so the MTA refuses the modification on arrival and
        // the message goes out unchanged.
        //
        // Naming macros is the exception, and the only one, because it changes nothing about the message.
        // An MTA that will not grant SMFIF_SETSYMLIST sends its own configured macro set instead, which
        // is what every filter got before it could ask.
        ThrowIfUnsupportedActionRequirements(grantedActions, requestedActions & ~ActionDiscriminator.SetMacroList);

        var actionRequirements = requestedActions & grantedActions;

        // Events quietly degrade where actions do not. An event the MTA will not suppress merely arrives
        // anyway, and the callback that was never overridden answers Continue, which is what happened
        // before any of this was negotiated.
        var contentBypass = config.ToContentBypassDiscriminator()
                            & ProtocolVersionCapabilities.ContentBypassFor(negotiatedVersion)
                            & request.MtaSupportedContentBypass;

        ThrowIfLeadingHeaderSpaceWithheld(config.DeliveryRequirements, contentBypass);

        var response = new OptionNegotiationResponsePacket
        {
            MilterProtocolVersion = negotiatedVersion,
            MilterRequiredActions = actionRequirements,
            MilterUndesiredProtocolContent = contentBypass,
            MilterRequestedMacros = actionRequirements.HasFlag(ActionDiscriminator.SetMacroList) ? config.MacroRequirements : null,
        };

        var negotiation = new ProtocolNegotiationOutcome(negotiatedVersion, actionRequirements, contentBypass);

        return new(AsyncEnumerableEx.Return<ResponsePacket>(response).Finally(() => TransitionToNextState(negotiation)));
    }

    // Everything below this point degrades quietly against an older MTA, which is right for a filter that
    // loses nothing by it. A filter that genuinely uses version 6 loses something real - a skip masked
    // into a Continue, header values silently trimmed - and would carry on looking like it worked. The
    // requirement is derived from what the filter actually uses, so this only fires for one that means it.
    private static void ThrowIfBelowFilterRequirement(Version negotiatedVersion, ProtocolVersionRequirement requirement)
    {
        if (negotiatedVersion.Major >= (int)requirement)
            return;

        throw new NotSupportedException(
            $"The negotiated Milter protocol version is {negotiatedVersion.Major}, but this filter uses "
            + $"features that need version {(int)requirement}. Upgrade the MTA, or configure it to speak a "
            + "higher milter_protocol.");
    }

    // The one event flag whose loss is not a degradation, and the reason this check survives the derived
    // version requirement rather than being covered by it. Without SMFIP_HDR_LEADSPC the MTA trims the
    // leading space from every header value on the way in and puts one back on the way out; with it, the
    // filter sees and writes the bytes as they stand. A filter that asked for it and did not get it reads
    // different bytes than it was written against, on every header of every message, and nothing says so.
    //
    // Nothing derives this flag - it cannot be, since no code distinguishes a filter that wants byte
    // exactness from one that does not - so a filter that names it has said something deliberate.
    private static void ThrowIfLeadingHeaderSpaceWithheld(DeliveryRequirements requirements, ContentBypassDiscriminator negotiatedContentBypass)
    {
        if (!requirements.HasFlag(DeliveryRequirements.LeadingHeaderSpace))
            return;

        if (negotiatedContentBypass.HasFlag(ContentBypassDiscriminator.LeadingHeaderSpace))
            return;

        throw new NotSupportedException(
            "This filter declared DeliveryRequirements.LeadingHeaderSpace, which the negotiation did not "
            + "grant - the MTA either withheld it or speaks a version that does not define it. Header "
            + "values would arrive trimmed and be written back with a space the filter never wrote, so "
            + "the filter would not see the bytes it was written against.");
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

    // This state is the only place the outcome is ever computed, and nothing transitions back here - not
    // even quit-for-new-connection, which keeps the negotiation and only drops the replay history. So
    // the value is written once and handed on, which is what a constructor argument says and a property
    // on the machine would not.
    private async Task TransitionToNextState(ProtocolNegotiationOutcome negotiation)
    {
        await stateMachine.TransitionTo(NegotiatedStateFactory.Create(stateMachine, config, negotiation)).ConfigureAwait(false);
    }
}
