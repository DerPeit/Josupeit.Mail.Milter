// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Accepts the message and stops consulting the filter about it.
/// </summary>
/// <remarks>
/// The MTA sends no further events for this message, so every callback that would still have run is
/// skipped - including <see cref="IMilterCallbacks.OnEndOfBody"/>, which means accepting early gives up
/// the chance to modify the message. Reached through <see cref="CommitActions.Accept"/>.
/// </remarks>
public sealed record AcceptAction : CommitAction
{
    private AcceptAction()
    {
    }

    /// <summary>
    /// The single shared instance. This action carries no state, so there is never a reason for another.
    /// </summary>
    public static AcceptAction Instance { get; } = new();

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private static IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return AcceptMessageResponsePacket.Instance;
    }
}
