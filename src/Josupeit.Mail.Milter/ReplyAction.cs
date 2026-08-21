// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

/// <summary>
/// Rejects the message with an SMTP reply the filter chose itself.
/// </summary>
/// <remarks>
/// The same outcome as <see cref="RejectAction"/> or <see cref="DeferAction"/> depending on the code,
/// but with wording the sender actually reads. Reached through <see cref="CommitActions.Reply"/>, which
/// is where the code and text are validated.
/// </remarks>
public sealed record ReplyAction : CommitAction
{
    internal ReplyAction(int code, string text)
    {
        Code = code;
        Text = text;
    }

    /// <summary>
    /// The SMTP status code to answer with, between 400 and 599.
    /// </summary>
    /// <remarks>
    /// A 4xx code defers and a 5xx code rejects, which is the whole difference between the two outcomes
    /// as far as the sender is concerned.
    /// </remarks>
    public int Code { get; }

    /// <summary>
    /// The text that accompanies <see cref="Code"/> in the reply.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new ReplyWithCodeResponsePacket
        {
            SmtpCode = Code.ToString(CultureInfo.InvariantCulture),
            Text = Text,
        };
    }
}
