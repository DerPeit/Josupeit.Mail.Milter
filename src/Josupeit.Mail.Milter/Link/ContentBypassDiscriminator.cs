using System;

namespace Josupeit.Mail.Milter.Link;

// These values map to the SMFIP_ constants.
[Flags]
public enum ContentBypassDiscriminator : uint
{
    None = 0x00,
    NoConnect = 0x01, // SMFIP_NOCONNECT
    NoHelo = 0x02, // SMFIP_NOHELO
    NoSender = 0x04, // SMFIP_NOMAIL
    NoRecipient = 0x08, // SMFIP_NORCPT
    NoHeader = 0x20, // SMFIP_NOHDRS
    NoEndOfHeaders = 0x40, // SMFIP_NOEOH
    NoBody = 0x10, // SMFIP_NOBODY
    All = NoConnect | NoHelo | NoSender | NoRecipient | NoBody | NoHeader | NoEndOfHeaders,
}