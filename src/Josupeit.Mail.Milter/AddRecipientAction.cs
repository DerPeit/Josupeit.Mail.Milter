// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

// Both forms of the same instruction, kept in one file because choosing between them is the whole
// point and reading either alone invites the wrong choice.
//
// The names carry the protocol version that introduced each, which is unusual enough to justify: they
// are two separate rights on the wire - SMFIF_ADDRCPT and SMFIF_ADDRCPT_PAR - an MTA may grant one
// without the other, and the source generator derives which one a filter needs from the type of the
// action alone. The version is the distinction, so the name says so rather than leaving a reader to
// infer it from a suffix like "WithParameters" that sounds optional.
// ReSharper disable InconsistentNaming

/// <summary>
/// Adds an envelope recipient to the message.
/// </summary>
/// <remarks>
/// This changes where the message is delivered, not what its headers say: the added address appears in
/// no <c>To</c> or <c>Cc</c> header unless the filter puts it there as well. Requires
/// <see cref="ActionRequirements.AddRecipients"/>. Reached through
/// <see cref="ModificationActions.AddRecipient(string)"/>, which is where the address is validated.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.AddRecipients))]
public record AddRecipientAction_V2 : ModificationAction
{
    internal AddRecipientAction_V2(string recipient)
    {
        Recipient = recipient;
    }

    /// <summary>
    /// The address to add to the envelope.
    /// </summary>
    public string Recipient { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new AddRecipientResponsePacket
        {
            Recipient = Recipient,
        };
    }
}

/// <summary>
/// Adds an envelope recipient together with the ESMTP arguments that travel with it.
/// </summary>
/// <remarks>
/// <para>
/// The same instruction as <see cref="AddRecipientAction_V2"/> carrying one field more, which is why it
/// derives from it - but a different right: it requires
/// <see cref="ActionRequirements.AddRecipientsWithParameters"/> rather than
/// <see cref="ActionRequirements.AddRecipients"/>, and an MTA speaking protocol version 6. Reached
/// through <see cref="ModificationActions.AddRecipient(string, string)"/>.
/// </para>
/// <para>
/// The generator reads the right off the nearest marker up the type hierarchy, so it sees this one and
/// not the base's. That also means a value held in a variable typed as the base declares the narrower
/// right: build these through the factory and let the type stay visible, rather than widening a
/// declaration to <see cref="AddRecipientAction_V2"/> along the way.
/// </para>
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.AddRecipientsWithParameters), MinimumProtocolVersion = 6)]
public sealed record AddRecipientAction_V6 : AddRecipientAction_V2
{
    internal AddRecipientAction_V6(string recipient, string eSmtpArguments)
        : base(recipient)
    {
        ESmtpArguments = eSmtpArguments;
    }

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
        yield return new AddRecipientResponsePacket
        {
            Recipient = Recipient,
            ESmtpArguments = ESmtpArguments,
        };
    }
}
