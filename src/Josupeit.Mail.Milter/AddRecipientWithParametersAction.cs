// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Adds an envelope recipient together with the ESMTP arguments that travel with it.
/// </summary>
/// <remarks>
/// A separate action from <see cref="AddRecipientAction"/> rather than an optional argument on it,
/// because the two need different rights from the MTA and the source generator derives those from the
/// type of the action alone - an argument that only sometimes carries parameters would leave it
/// guessing. Requires <see cref="ActionRequirements.AddRecipientsWithParameters"/> and an MTA speaking
/// protocol version 6. Reached through <see cref="ModificationActions.AddRecipientWithParameters"/>.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.AddRecipientsWithParameters))]
public sealed record AddRecipientWithParametersAction : ModificationAction
{
    internal AddRecipientWithParametersAction(string recipient, string eSmtpArguments)
    {
        Recipient = recipient;
        ESmtpArguments = eSmtpArguments;
    }

    /// <summary>
    /// The address to add to the envelope.
    /// </summary>
    public string Recipient { get; }

    /// <summary>
    /// The ESMTP arguments to send with it, such as <c>NOTIFY=NEVER</c>.
    /// </summary>
    public string ESmtpArguments { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new AddRecipientWithParametersResponsePacket
        {
            Recipient = Recipient,
            ESmtpArguments = ESmtpArguments,
        };
    }
}
