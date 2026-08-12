// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

/// <summary>
/// What the MTA reports about the client's <c>HELO</c> or <c>EHLO</c>.
/// </summary>
/// <param name="Hostname">
/// The name the client announced itself as. This is the client's own claim and nothing more - the MTA
/// does not verify that it resolves, or that it matches the address the client connected from.
/// </param>
public readonly record struct ClientHelloInformation(string Hostname);