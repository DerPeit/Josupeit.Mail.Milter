// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Tells the MTA that the filter is still working, so it restarts its reply timeout instead of giving up
/// on the connection. It decides nothing about the message, so it can be yielded from
/// <see cref="IMilterCallbacks.OnEndOfBody"/> as often as needed before the commit action.
/// </summary>
public sealed record ProgressAction : Action
{
    public static ProgressAction Instance { get; } = new();

    private ProgressAction()
    {
    }

    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private static IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return ProgressResponsePacket.Instance;
    }
}
