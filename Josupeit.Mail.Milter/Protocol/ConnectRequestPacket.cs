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

    public required AddressFamily Family { get; init; }

    public required int Port { get; init; }

    public required IPAddress Address { get; init; }
    
    protected internal override bool Accept(RequestPacketVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        return visitor.VisitConnectRequestPacket(this);
    }
}