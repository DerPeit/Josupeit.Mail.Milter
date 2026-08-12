// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Turns a frame off the wire into the typed packet it represents.
/// </summary>
/// <remarks>
/// The seam between the two lower layers. Substitute an implementation to change how frames are read -
/// to accept a command this library does not, for instance.
/// </remarks>
public interface IRequestPacketFactory
{
    /// <summary>
    /// Creates the packet a frame describes.
    /// </summary>
    /// <param name="frame">
    /// The frame to read. Its payload is pooled memory owned by the caller, so anything the packet keeps
    /// has to be copied out rather than referenced.
    /// </param>
    /// <returns>The packet.</returns>
    RequestPacket CreateRequestPacket(RequestFrame frame);
}
