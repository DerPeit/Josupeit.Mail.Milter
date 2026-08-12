using System;
using Josupeit.Mail.Milter.Generator;

namespace Josupeit.Mail.Milter;

[Flags]
[ProtocolRequirementsMarker]
public enum ProtocolRequirements
{
    Connect = 1,
    ClientHello = 2,
    Sender = 4,
    Recipient = 8,
    Header = 16,
    EndOfHeaders = 32,
    Body = 64,
    EndOfBody = 0, // cannot be excluded
    All = Connect | ClientHello | Sender | Recipient | Header | EndOfHeaders | Body | EndOfBody,
}