// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;

namespace Josupeit.Mail.Milter.Link;

public readonly record struct RequestFrame(CommandCode CommandCode, IMemoryOwner<byte>? Memory, Range DataRange) : IDisposable
{
    internal ReadOnlyMemory<byte> Data => Memory?.Memory[DataRange] ?? ReadOnlyMemory<byte>.Empty;

    public void Dispose()
    {
        Memory?.Dispose();
    }
}
