// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public abstract record Action
{
    private protected Action()
    {
    }

    internal IAsyncEnumerable<ResponsePacket> CreateResponsePackets()
    {
        return CreateResponsePackets(CancellationToken.None);
    }

    protected abstract IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken);
}