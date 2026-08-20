// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Rejects the message permanently, which the MTA reports to the sender as a 5xx error.
/// </summary>
/// <remarks>
/// A permanent rejection tells the sender not to try again, so a legitimate message refused this way is
/// lost rather than delayed - use <see cref="DeferAction"/> when the filter is merely unsure or
/// temporarily unable to decide. <see cref="CommitActions.Reply"/> sends a rejection with wording of the
/// filter's own choosing. Reached through <see cref="CommitActions.Reject"/>.
/// </remarks>
public sealed record RejectAction : CommitAction
{
    private RejectAction()
    {
    }

    /// <summary>
    /// The single shared instance. This action carries no state, so there is never a reason for another.
    /// </summary>
    public static RejectAction Instance { get; } = new();

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private static IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return RejectMessageResponsePacket.Instance;
    }
}
