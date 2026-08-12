// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record ChangeHeaderAction : ModificationAction
{
    internal ChangeHeaderAction(string name, string value, int index)
    {
        Name = name;
        Value = value;
        Index = index;
    }

    public string Name { get; }

    public string Value { get; }

    public int Index { get; }

    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new ChangeOrDeleteHeaderResponsePacket
        {
            Index = (uint)Index,
            Name = Name,
            Value = Value,
        };
    }
}