// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session;

/// <summary>
/// One milter conversation, from the option negotiation to the connection closing.
/// </summary>
/// <remarks>
/// A session spans a whole connection and therefore possibly several messages, since an MTA reuses a
/// connection. That is a different lifetime from the filter's own: the context is rebuilt per message,
/// while the session outlives all of them.
/// </remarks>
public interface ISession
{
    /// <summary>
    /// Runs the conversation until the MTA quits, the connection fails, or the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">A token that ends the session early.</param>
    /// <returns>A task that completes when the conversation has ended.</returns>
    Task Run(CancellationToken cancellationToken);
}
