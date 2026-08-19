// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

/// <summary>
/// What the MTA reports about an SMTP command it does not recognise.
/// </summary>
/// <param name="CommandText">
/// The command line exactly as the client sent it, verb and arguments together. The MTA had no grammar
/// to apply to it, so nothing here has been parsed or validated - it is attacker-controlled text, and a
/// filter that logs it or matches on it should treat it as such.
/// </param>
public readonly record struct UnknownCommandInformation(string CommandText);
