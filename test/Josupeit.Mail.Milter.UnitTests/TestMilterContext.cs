// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.UnitTests;

// A hand-written context that relies entirely on the default callback implementations of IMilterCallbacks.
internal sealed class TestMilterContext : IMilterContext
{
    public static ProtocolRequirements ProtocolRequirements { get; set; } = ProtocolRequirements.All;

    public static ActionRequirements ActionRequirements { get; set; } = ActionRequirements.None;
}
