// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Replaces the value of a header already on the message.
/// </summary>
/// <remarks>
/// Requires <see cref="ActionRequirements.ChangeHeaders"/>. Reached through
/// <see cref="ModificationActions.ChangeHeader"/>, which is where the name and value are validated.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.ChangeHeaders))]
public sealed record ChangeHeaderAction : ModificationAction
{
    internal ChangeHeaderAction(string name, string value, int index)
    {
        Name = name;
        Value = value;
        Index = index;
    }

    /// <summary>
    /// The name of the header to change, without the separating colon.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The value to put in its place.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Which occurrence of <see cref="Name"/> to change, counting from zero.
    /// </summary>
    /// <remarks>
    /// A message may carry the same header more than once - <c>Received</c> always does. This counts from
    /// zero, unlike the wire field it becomes, which counts from one; the translation happens on the way
    /// out.
    /// </remarks>
    public int Index { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new ChangeOrDeleteHeaderResponsePacket
        {
            Index = (uint)Index,
            Name = Name,
            Value = Value,
        };
    }
}
