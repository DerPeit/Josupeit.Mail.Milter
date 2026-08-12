// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// A background worker that turns the incoming byte stream into request frames.
/// </summary>
/// <remarks>
/// It runs alongside the session rather than being driven by it: frames are read as fast as the peer
/// sends them and handed over through a bounded channel, so reading and handling do not wait on each
/// other beyond the one frame of slack that channel allows.
/// </remarks>
public interface IRequestParser
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
