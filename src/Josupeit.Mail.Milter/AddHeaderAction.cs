// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Generator;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Appends a header to the message.
/// </summary>
/// <remarks>
/// Requires <see cref="ActionRequirements.AddHeaders"/>. Reached through
/// <see cref="ModificationActions.AddHeader"/>, which is where the name and value are validated.
/// </remarks>
[MilterActionMarker(nameof(ActionRequirements.AddHeaders))]
public sealed record AddHeaderAction : ModificationAction
{
    internal AddHeaderAction(string name, string value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// The name of the header to add, without the separating colon.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The value of the header to add.
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
        yield return new AddHeaderResponsePacket
        {
            Name = Name,
            Value = Value,
        };
    }
}