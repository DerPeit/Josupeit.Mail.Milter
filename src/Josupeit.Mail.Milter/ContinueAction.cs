// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Decides nothing and lets the MTA carry on to the next event.
/// </summary>
/// <remarks>
/// The answer every callback gives by default, and the only one that leaves the message's fate open.
/// Reached through <see cref="CommitActions.Continue"/>.
/// </remarks>
public sealed record ContinueAction : CommitAction
{
    /// <summary>
    /// The single shared instance. This action carries no state, so there is never a reason for another.
    /// </summary>
    public static ContinueAction Instance { get; } = new();

    private ContinueAction()
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
        yield return ContinueProcessingResponsePacket.Instance;
    }
}