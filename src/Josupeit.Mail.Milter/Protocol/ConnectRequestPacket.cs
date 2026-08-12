using System;
using System.Net;
using System.Net.Sockets;

namespace Josupeit.Mail.Milter.Protocol;

public sealed class ConnectRequestPacket : RequestPacket
{
    internal ConnectRequestPacket()
    {
    }

    public required string HostName { get; init; }

    /// <summary>
    /// The endpoint the client connected from, or <see langword="null"/> when the MTA reported none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an <see cref="IPEndPoint"/> for a remote client and a <see cref="UnixDomainSocketEndPoint"/>
    /// for local submission.
    /// </para>
    /// <para>
    /// <see langword="null"/> is a genuine value here rather than a missing one, which is why this is
    /// nullable at all: SMFIA_UNKNOWN carries neither a port nor an address, and an internet address may
    /// arrive as the literal "unknown" when the MTA has none (Postfix sends that for
    /// XCLIENT ADDR=[UNAVAILABLE]). No <see cref="EndPoint"/> represents "there is no address", so we
    /// cannot express those cases any other way.
    /// </para>
    /// </remarks>
    public EndPoint? EndPoint { get; init; }

    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitConnectRequestPacket(this);
    }
}
