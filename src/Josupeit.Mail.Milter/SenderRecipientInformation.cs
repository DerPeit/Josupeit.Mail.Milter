// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;

namespace Josupeit.Mail.Milter;

/// <summary>
/// What the MTA reports about a <c>MAIL FROM</c> or a <c>RCPT TO</c>.
/// </summary>
/// <param name="AddressString">
/// The envelope address exactly as it appeared on the command, angle brackets included - so
/// <c>&lt;user@example.com&gt;</c> rather than <c>user@example.com</c>. It is the raw argument and not a
/// parsed address, so a filter that needs the parts has to parse it.
/// </param>
/// <param name="ESmtpArguments">
/// The ESMTP parameters that followed the address, such as <c>SIZE=</c> or <c>BODY=</c>, each as its own
/// unparsed string. Empty when the command carried none.
/// </param>
public readonly record struct SenderRecipientInformation(string AddressString, IReadOnlyList<string> ESmtpArguments);