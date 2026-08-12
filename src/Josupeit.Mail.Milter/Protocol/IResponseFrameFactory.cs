// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Turns a typed packet into the frame that carries it on the wire.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="IRequestPacketFactory"/>, and the other seam between the two lower
/// layers.
/// </remarks>
public interface IResponseFrameFactory
{
    /// <summary>
    /// Creates the frame for a packet.
    /// </summary>
    /// <param name="response">The packet to encode.</param>
    /// <returns>The frame, holding pooled memory that its consumer disposes once written.</returns>
    ResponseFrame CreateResponseFrame(ResponsePacket response);
}
