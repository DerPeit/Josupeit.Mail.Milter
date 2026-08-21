// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator.Markers;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Removes a header from the message.
/// </summary>
/// <remarks>
/// Requires <see cref="ActionRequirements.DeleteHeaders"/>. Reached through
/// <see cref="ModificationActions.DeleteHeader"/>, which is where the name is validated. On the wire
/// this is a change to an empty value, which is how the protocol expresses a deletion.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.DeleteHeaders))]
public sealed record DeleteHeaderAction : ModificationAction
{
    internal DeleteHeaderAction(string name, int index)
    {
        Name = name;
        Index = index;
    }

    /// <summary>
    /// The name of the header to remove, without the separating colon.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Which occurrence of <see cref="Name"/> to remove, counting from zero.
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
            Value = string.Empty,
        };
    }
}
