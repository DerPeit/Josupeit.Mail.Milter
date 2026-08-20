// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// One response to the MTA, as a type that names what it is rather than as bytes.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="RequestPacket"/>. An <see cref="Action"/> produces these, and
/// <see cref="ResponseFrameFactory"/> turns them into <see cref="Link.ResponseFrame"/>s. Dispatch is by
/// visitor - see <see cref="ResponsePacketVisitor"/> - and the hierarchy is open, as it is on the
/// request side, so a packet type of one's own is possible.
/// <para>
/// The same shape applies: a packet of one's own needs a visitor of one's own, so derive
/// <see cref="ResponsePacketVisitor"/>, give it the method, and have <see cref="Accept"/> recognise the
/// visitor and call it. Encoding one is reachable here in a way it is not on the request side, because
/// <see cref="ResponseFrameFactory"/> hands its own visitor out to be derived from rather than keeping
/// it to itself - and an encoder meeting a packet it cannot place throws rather than emitting nothing.
/// </para>
/// </remarks>
public abstract class ResponsePacket
{
    /// <summary>
    /// Dispatches to the visitor method matching this packet's type.
    /// </summary>
    /// <param name="visitor">The visitor to dispatch to.</param>
    /// <returns>Whatever the visitor returned; <see langword="false"/> ends the dispatch.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="visitor"/> is
    /// <see langword="null"/>.</exception>
    protected internal abstract bool Accept(ResponsePacketVisitor visitor);
}
