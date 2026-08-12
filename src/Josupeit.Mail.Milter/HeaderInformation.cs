// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

/// <summary>
/// One header of the message under inspection.
/// </summary>
/// <param name="Name">The header name, without the separating colon.</param>
/// <param name="Value">
/// The header value as the MTA has it. A header folded across several lines arrives here unfolded.
/// </param>
public readonly record struct HeaderInformation(string Name, string Value);