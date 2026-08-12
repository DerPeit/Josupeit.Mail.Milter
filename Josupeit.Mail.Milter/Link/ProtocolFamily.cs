namespace Josupeit.Mail.Milter.Link;

// These values map to the SMFIA_ constants.
public enum ProtocolFamily : byte
{
    Unspecified = 0,
    Unknown = (byte)'U', // SMFIA_UNKNOWN
    UnixSocket = (byte)'L', // SMFIA_UNIX
    Ip4 = (byte)'4', // SMFIA_INET
    Ip6 = (byte)'6', // FMFIA_INET6
}