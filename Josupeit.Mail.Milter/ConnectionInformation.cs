using System.Net;
using System.Net.Sockets;

namespace Josupeit.Mail.Milter;

/// <param name="Hostname">The hostname the MTA resolved for the connecting client.</param>
/// <param name="EndPoint">
/// The endpoint the client connected from, or <see langword="null"/> when the MTA reported none.
/// This is an <see cref="IPEndPoint"/> for a remote client and a <see cref="UnixDomainSocketEndPoint"/>
/// for local submission. It is <see langword="null"/> when the MTA has no address for the connection,
/// which it reports either as an unknown protocol family or as the literal address "unknown".
/// </param>
public readonly record struct ConnectionInformation(string Hostname, EndPoint? EndPoint);
