using System.Net;
using System.Net.Sockets;

namespace Josupeit.Mail.Milter;

public readonly record struct ConnectionInformation(string Hostname, AddressFamily Family, int Port, IPAddress Address);