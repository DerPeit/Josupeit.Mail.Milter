// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public interface ILinkReader
{
    ValueTask<uint> ReadUInt32(CancellationToken cancellationToken);

    ValueTask<CommandCode> ReadCommandCode(CancellationToken cancellationToken);

    ValueTask<(IMemoryOwner<byte>? Memory, Range DataRange)> ReadData(uint length, CancellationToken cancellationToken);
}