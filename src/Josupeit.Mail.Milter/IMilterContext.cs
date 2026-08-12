// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Generator;

namespace Josupeit.Mail.Milter;

[MilterContextMarker]
public interface IMilterContext : IMilterCallbacks
{
    static abstract ProtocolRequirements ProtocolRequirements { get; }

    static abstract ActionRequirements ActionRequirements { get; }
}
