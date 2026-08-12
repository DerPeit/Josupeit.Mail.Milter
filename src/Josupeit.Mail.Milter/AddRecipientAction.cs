// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Adds an envelope recipient to the message.
/// </summary>
/// <remarks>
/// This changes where the message is delivered, not what its headers say: the added address appears in
/// no <c>To</c> or <c>Cc</c> header unless the filter puts it there as well. Requires
/// <see cref="ActionRequirements.AddRecipients"/>. Reached through
/// <see cref="ModificationActions.AddRecipient"/>, which is where the address is validated.
/// </remarks>
public sealed record AddRecipientAction : ModificationAction
{
    internal AddRecipientAction(string recipient)
    {
        Recipient = recipient;
    }

    /// <summary>
    /// The address to add to the envelope.
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
        yield return new AddRecipientResponsePacket
        {
            Recipient = Recipient,
        };
    }
}