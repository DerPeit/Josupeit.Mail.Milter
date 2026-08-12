// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Accepts the message on the wire and then silently drops it.
/// </summary>
/// <remarks>
/// The sender is told the message was delivered and no recipient ever sees it, which is the point:
/// unlike <see cref="RejectAction"/> it gives a sender no signal at all about what the filter did.
/// Reached through <see cref="CommitActions.Discard"/>.
/// </remarks>
public sealed record DiscardAction : CommitAction
{
    /// <summary>
    /// The single shared instance. This action carries no state, so there is never a reason for another.
    /// </summary>
    public static DiscardAction Instance { get; } = new();

    private DiscardAction()
    {
    }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private static IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return DiscardMessageResponsePacket.Instance;
    }
}