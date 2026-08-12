// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter.Link;

/// <summary>
/// What kind of address the connection information carries.
/// </summary>
/// <remarks>
/// These map to the <c>SMFIA_</c> constants. The family decides how the rest of the connect frame is
/// read: an address and port follow for the two internet families, a path for a Unix socket, and nothing
/// at all for <see cref="Unknown"/>.
/// </remarks>
public enum ProtocolFamily : byte
{
    /// <summary>
    /// No family. Not a value the protocol defines; it marks a frame whose family has not been set.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The MTA has no address for the connection (<c>SMFIA_UNKNOWN</c>).
    /// </summary>
    /// <remarks>
    /// Neither an address nor a port follows. This is a real answer rather than a missing one - it is what
    /// an MTA says about a connection it cannot describe.
    /// </remarks>
    Unknown = (byte)'U',

    /// <summary>
    /// A Unix domain socket, which is how local submission arrives (<c>SMFIA_UNIX</c>).
    /// </summary>
    UnixSocket = (byte)'L',

    /// <summary>
    /// An IPv4 address (<c>SMFIA_INET</c>).
    /// </summary>
    Ip4 = (byte)'4',

    /// <summary>
    /// An IPv6 address (<c>SMFIA_INET6</c>).
    /// </summary>
    Ip6 = (byte)'6',
}
