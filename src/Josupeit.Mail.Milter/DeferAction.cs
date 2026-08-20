// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Rejects the message temporarily, which the MTA reports to the sender as a 4xx error.
/// </summary>
/// <remarks>
/// A sending MTA treats this as "try again later" and queues the message, so nothing is lost. This is
/// the right answer when the filter cannot reach something it needs to decide with. Reached through
/// <see cref="CommitActions.Defer"/>.
/// </remarks>
public sealed record DeferAction : CommitAction
{
    private DeferAction()
    {
    }

    /// <summary>
    /// The single shared instance. This action carries no state, so there is never a reason for another.
    /// </summary>
    public static DeferAction Instance { get; } = new();

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private static IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return DeferMessageResponsePacket.Instance;
    }
}
