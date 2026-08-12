using System.Collections.Generic;

namespace Josupeit.Mail.Milter;

public readonly record struct SenderRecipientInformation(string AddressString, IReadOnlyList<string> ESmtpArguments);