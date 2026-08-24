// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// One request from the MTA, parsed out of a frame into a type that names what it is.
/// </summary>
/// <remarks>
/// The layer above the wire: <see cref="RequestPacketFactory"/> builds these from
/// <see cref="Link.RequestFrame"/>s, and everything past that point works with the typed packet rather
/// than with bytes. Dispatch is by visitor - see <see cref="RequestPacketVisitor"/> - and the hierarchy
/// is open, so a derived factory can introduce a packet type of its own.
/// <para>
/// A packet of one's own needs a visitor of one's own, which is the shape of the pattern rather than a
/// restriction: a new kind of packet needs a new method to handle it, and no existing one would fit.
/// Derive <see cref="RequestPacketVisitor"/>, give it that method, and have <see cref="Accept"/>
/// recognise the visitor and call it - returning <see langword="false"/> for any other visitor, which is
/// what that return value is for.
/// </para>
/// <para>
/// A visitor of this library's meeting such a packet says so: the session controller throws rather than
/// carrying on, because a packet nothing answered would otherwise leave the session waiting on a reply
/// that was never composed.
/// </para>
/// </remarks>
public abstract class RequestPacket
{
    /// <summary>
    /// Dispatches to the visitor method matching this packet's type.
    /// </summary>
    /// <param name="visitor">The visitor to dispatch to.</param>
    /// <returns>Whatever the visitor returned; <see langword="false"/> ends the dispatch.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="visitor"/> is
    /// <see langword="null"/>.</exception>
    protected internal abstract bool Accept(RequestPacketVisitor visitor);
}
