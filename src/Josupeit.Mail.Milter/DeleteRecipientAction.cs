// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Removes an envelope recipient from the message.
/// </summary>
/// <remarks>
/// The address must be one the MTA already has on the envelope, and it has to be spelled the way the
/// MTA spelled it in <see cref="IMilterCallbacks.OnRecipient"/>. Removing the last recipient leaves the
/// MTA with nowhere to deliver, so a filter that may empty the envelope should reject the message
/// instead. Requires <see cref="ActionRequirements.DeleteRecipients"/>. Reached through
/// <see cref="ModificationActions.DeleteRecipient"/>, which is where the address is validated.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.DeleteRecipients))]
public sealed record DeleteRecipientAction : ModificationAction
{
    internal DeleteRecipientAction(string recipient)
    {
        Recipient = recipient;
    }

    /// <summary>
    /// The address to remove from the envelope.
    /// </summary>
    public string Recipient { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new DeleteRecipientResponsePacket
        {
            Recipient = Recipient,
        };
    }
}
