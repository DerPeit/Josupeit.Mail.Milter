// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Something a filter tells the MTA in reply to an event.
/// </summary>
/// <remarks>
/// Three kinds sit under this type. A <see cref="CommitAction"/> decides the message's fate, a
/// <see cref="ModificationAction"/> changes the message, and <see cref="ProgressAction"/> does neither -
/// it only asks for more time. They are built through the <see cref="CommitActions"/> and
/// <see cref="ModificationActions"/> factories, which is the only way: the constructors are internal and
/// the properties get-only, so neither <see langword="new"/> nor a <see langword="with"/> expression can
/// get past the validation the factories perform.
/// </remarks>
public abstract record Action
{
    private protected Action()
    {
    }

    internal IAsyncEnumerable<ResponsePacket> CreateResponsePackets()
    {
        return CreateResponsePackets(CancellationToken.None);
    }

    /// <summary>
    /// Produces the protocol packets that carry this action to the MTA.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the production of the packets.</param>
    /// <returns>The packets to send, in the order they must be sent.</returns>
    protected abstract IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken);
}