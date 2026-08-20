// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

/// <summary>
/// One header of the message under inspection.
/// </summary>
/// <param name="Name">The header name, without the separating colon.</param>
/// <param name="Value">
/// The header value as the MTA has it. A header folded across several lines arrives here unfolded.
/// <para>
/// Whether the space after the colon is part of this depends on
/// <see cref="DeliveryRequirements.LeadingHeaderSpace"/>. Without it the MTA strips that space on the
/// way in and puts one back in front of whatever the filter writes, so a value reads and writes as
/// <c>hello</c>. With it the MTA does neither, the value reads as <c>&#32;hello</c>, and a filter that
/// writes one back without the space produces a header with none. This library never adds or removes
/// it in either mode - that is what makes the two halves symmetric - so the byte belongs to the filter
/// as soon as the flag is declared.
/// </para>
/// </param>
public readonly record struct HeaderInformation(string Name, string Value);
