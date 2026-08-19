// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Stops the MTA sending any more of the event just answered, leaving the message otherwise untouched.
/// </summary>
/// <remarks>
/// <para>
/// Only meaningful for an event that repeats: a body chunk, a header, a recipient. A filter that has
/// seen enough of the body says so once and the remaining chunks never cross the socket - which on a
/// large message is the difference between reading a megabyte and reading sixty-four kilobytes.
/// </para>
/// <para>
/// This decides nothing about the message. Processing carries on exactly as it would have; all that
/// stops is the reporting, and <see cref="IMilterCallbacks.OnEndOfBody"/> still runs.
/// </para>
/// <para>
/// Requires an MTA speaking protocol version 6 that granted <c>SMFIP_SKIP</c>. When it did not, this
/// quietly becomes <see cref="CommitActions.Continue"/> - losing an optimisation must not lose mail.
/// Answering it from an event that does not repeat is a programming error and throws.
/// </para>
/// </remarks>
// The skip action contributes no ActionRequirements flag - SMFIP_SKIP is an event bit, not an
// SMFIF_ action - but it still cannot be sent to an MTA below version 6.
[MilterActionMarker(MinimumProtocolVersion = 6)]
public sealed record SkipRemainingEventsAction : CommitAction
{
    internal static readonly SkipRemainingEventsAction Instance = new();

    private SkipRemainingEventsAction()
    {
    }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private static IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return SkipRemainingEventsResponsePacket.Instance;
    }
}
