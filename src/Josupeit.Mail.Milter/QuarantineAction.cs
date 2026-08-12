// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Holds the message in the MTA's queue instead of delivering it.
/// </summary>
/// <remarks>
/// The message is neither delivered nor bounced; it waits for an administrator to release or drop it,
/// which is the difference from <see cref="DiscardAction"/>. Postfix reports this as its HOLD action.
/// Requires <see cref="ActionRequirements.QuarantineMessage"/>. Reached through
/// <see cref="ModificationActions.QuarantineMessage"/>, which is where the reason is validated.
/// </remarks>
public sealed record QuarantineAction : ModificationAction
{
    internal QuarantineAction(string reason)
    {
        Reason = reason;
    }

    /// <summary>
    /// Why the message is being held, for whoever reviews the queue.
    /// </summary>
    /// <remarks>
    /// Postfix reads this off the wire and then logs its own fixed text instead, so against Postfix the
    /// reason is not observable in the log. It still travels.
    /// </remarks>
    public string Reason { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new QuarantineMessageResponsePacket
        {
            Reason = Reason,
        };
    }
}