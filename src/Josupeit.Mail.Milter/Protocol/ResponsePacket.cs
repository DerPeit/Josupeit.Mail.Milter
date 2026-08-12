// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// One response to the MTA, as a type that names what it is rather than as bytes.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="RequestPacket"/>. An <see cref="Action"/> produces these, and
/// <see cref="ResponseFrameFactory"/> turns them into <see cref="Link.ResponseFrame"/>s. Dispatch is by
/// visitor - see <see cref="ResponsePacketVisitor"/> - and the hierarchy is closed for the same reason.
/// </remarks>
public abstract class ResponsePacket
{
    private protected ResponsePacket()
    {
    }

    /// <summary>
    /// Dispatches to the visitor method matching this packet's type.
    /// </summary>
    /// <param name="visitor">The visitor to dispatch to.</param>
    /// <returns>Whatever the visitor returned; <see langword="false"/> ends the dispatch.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="visitor"/> is
    /// <see langword="null"/>.</exception>
    protected internal abstract bool Accept(ResponsePacketVisitor visitor);
}
