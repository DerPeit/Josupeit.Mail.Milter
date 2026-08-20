// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Session;

namespace Josupeit.Mail.Milter.UnitTests;

// The outcome a post-negotiation state needs in order to exist at all, for the tests that are not about
// negotiation. Version 2 with nothing granted: every event is answered and no version 6 behaviour is on,
// which is what the library did before any of it was negotiable.
//
// This lived in the library as ProtocolNegotiationOutcome.NotNegotiated, and only existed because the
// session held an outcome during the window before one had been negotiated. The states take theirs as a
// constructor argument now, so there is no such window and the stand-in belongs to the tests that want
// one rather than to the shipped type.
internal static class TestNegotiation
{
    public static readonly ProtocolNegotiationOutcome Nothing = new(
        new Version(2, 0),
        ActionDiscriminator.None,
        ContentBypassDiscriminator.None);
}
