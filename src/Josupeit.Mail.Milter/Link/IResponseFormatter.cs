// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// A background worker that turns response frames into the outgoing byte stream.
/// </summary>
/// <remarks>
/// The mirror of <see cref="IRequestParser"/>, and it runs alongside the session for the same reason:
/// frames are taken from a bounded channel and written as fast as the peer accepts them, so deciding and
/// writing do not wait on each other. Each frame is completed once it has actually been written, which
/// is what lets the session know a response has left rather than merely been queued.
/// </remarks>
public interface IResponseFormatter
{
    /// <summary>
    /// Starts the worker and returns immediately.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the worker and waits for it to finish.
    /// </summary>
    /// <returns>A task that completes once the worker has ended.</returns>
    Task Stop();
}
