// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Inserts a header at a chosen position in the message.
/// </summary>
/// <remarks>
/// Requires <see cref="ActionRequirements.AddHeaders"/> - the same right as
/// <see cref="AddHeaderAction"/>, because the protocol has no separate flag for inserting and
/// libmilter guards both with <c>SMFIF_ADDHDRS</c>. Reached through
/// <see cref="ModificationActions.InsertHeader"/>, which is where the name and value are validated.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.AddHeaders))]
public sealed record InsertHeaderAction : ModificationAction
{
    internal InsertHeaderAction(int index, string name, string value)
    {
        Index = index;
        Name = name;
        Value = value;
    }

    /// <summary>
    /// How many headers the new one is placed after.
    /// </summary>
    /// <remarks>
    /// Counts from zero, so zero puts the header above every existing one - which is what a filter
    /// adding a <c>Received</c>-like trace header usually wants. The wire counts from zero too, unlike
    /// a header change, so the value travels unaltered.
    /// </remarks>
    public int Index { get; }

    /// <summary>
    /// The name of the header to insert, without the separating colon.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The value of the header to insert, as it goes to the MTA.
    /// A folded value breaks its lines with bare LFs, even if it was handed over with CRLFs.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new InsertHeaderResponsePacket
        {
            Index = (uint)Index,
            Name = Name,
            Value = Value,
        };
    }
}
