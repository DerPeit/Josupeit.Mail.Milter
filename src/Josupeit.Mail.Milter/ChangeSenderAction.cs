// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Replaces the envelope sender of the message.
/// </summary>
/// <remarks>
/// This is the address bounces go to, not the <c>From</c> header the recipient sees: changing it
/// leaves the visible authorship alone. Requires <see cref="ActionRequirements.ChangeSender"/> and an
/// MTA speaking protocol version 6. Reached through <see cref="ModificationActions.ChangeSender"/>,
/// which is where the address is validated.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.ChangeSender), MinimumProtocolVersion = 6)]
public sealed record ChangeSenderAction : ModificationAction
{
    internal ChangeSenderAction(string sender, string? eSmtpArguments)
    {
        Sender = sender;
        ESmtpArguments = eSmtpArguments;
    }

    /// <summary>
    /// The address to become the new envelope sender.
    /// </summary>
    public string Sender { get; }

    /// <summary>
    /// The ESMTP arguments to send with it, or <see langword="null"/> when there are none.
    /// </summary>
    public string? ESmtpArguments { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new ChangeSenderResponsePacket
        {
            Sender = Sender,
            ESmtpArguments = ESmtpArguments,
        };
    }
}
