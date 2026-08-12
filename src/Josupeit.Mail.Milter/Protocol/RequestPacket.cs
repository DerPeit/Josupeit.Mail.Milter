// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// One request from the MTA, parsed out of a frame into a type that names what it is.
/// </summary>
/// <remarks>
/// The layer above the wire: <see cref="RequestPacketFactory"/> builds these from
/// <see cref="Link.RequestFrame"/>s, and everything past that point works with the typed packet rather
/// than with bytes. Dispatch is by visitor - see <see cref="RequestPacketVisitor"/> - which is why the
/// hierarchy is closed: the constructor is <see langword="private protected"/>, so the set of packets is
/// exactly the set this assembly declares and a visitor can be sure it has covered all of them.
/// </remarks>
public abstract class RequestPacket
{
    private protected RequestPacket()
    {
    }

    /// <summary>
    /// Dispatches to the visitor method matching this packet's type.
    /// </summary>
    /// <param name="visitor">The visitor to dispatch to.</param>
    /// <returns>Whatever the visitor returned; <see langword="false"/> ends the dispatch.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="visitor"/> is
    /// <see langword="null"/>.</exception>
    protected internal abstract bool Accept(RequestPacketVisitor visitor);
}
