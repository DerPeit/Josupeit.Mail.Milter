// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Runtime.CompilerServices;

namespace Josupeit.Mail.Milter;

// Guards the strings a filter hands to the MTA. Each one ends up in a NUL-terminated field and, for
// headers and replies, in the delivered message, so a stray control character either cuts the field
// short or forges a line the sender never wrote.
internal static partial class WireText
{
    private const char FIRST_CONTROL_CHARACTER = (char)0;
    private const char LAST_CONTROL_CHARACTER = (char)31;

    // libmilter asks a folded header to break its lines with a bare LF and leaves the CR to the MTA, and
    // Postfix holds it to that: cleanup splits the value on LF, so a CR left in would reach the message as
    // part of the line. Only call this on a value ThrowIfNotHeaderValue passed, where every CR is followed
    // by an LF.
    public static string ToWireLineBreaks(string headerValue) => headerValue.Replace("\r\n", "\n", StringComparison.Ordinal);
}

// The extension block sits in a declaration of its own only because SA1201 has no rank for the C# 14
// syntax kind and sorts it below a field, so the two consts above look like they arrive too late. Fold
// the two declarations back together once StyleCop knows the kind.
internal static partial class WireText
{
    extension(ArgumentException)
    {
        public static void ThrowIfContainsControlCharacter(string text, [CallerArgumentExpression(nameof(text))] string? paramName = null)
        {
            if (text.AsSpan().ContainsAnyInRange(FIRST_CONTROL_CHARACTER, LAST_CONTROL_CHARACTER))
                throw new ArgumentException("The value must not contain control characters.", paramName);
        }

        // RFC 5322 ftext: printable US-ASCII except the colon that separates the name from the value.
        public static void ThrowIfNotHeaderName(string name, [CallerArgumentExpression(nameof(name))] string? paramName = null)
        {
            if (name.AsSpan().ContainsAnyExceptInRange('!', '~') || name.Contains(':', StringComparison.Ordinal))
                throw new ArgumentException("A header name must consist of printable ASCII characters other than a colon.", paramName);
        }

        // A header value may be folded (RFC 5322 section 2.2.3): a line break followed by whitespace continues
        // the header instead of starting a new one, which is how a DKIM signature or a SpamAssassin report is
        // written. So a break is allowed exactly where whitespace follows it, and the continuation line must
        // then carry something besides whitespace - a line of nothing but whitespace reads as the blank line
        // ending the headers to more than one parser. A tab is whitespace and allowed anywhere; every other
        // control character is not, and a CR only as the first half of a CRLF.
        public static void ThrowIfNotHeaderValue(string value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            var remaining = value.AsSpan();
            var isContinuation = false;

            while (true)
            {
                var lineBreakAt = remaining.IndexOf('\n');
                var line = lineBreakAt < 0 ? remaining : remaining[..lineBreakAt];

                if (lineBreakAt >= 0 && line is [.., '\r'])
                    line = line[..^1];

                var hasControlCharacter = line.ContainsAnyInRange(FIRST_CONTROL_CHARACTER, (char)('\t' - 1))
                    || line.ContainsAnyInRange((char)('\t' + 1), LAST_CONTROL_CHARACTER);

                var isFolded = line is [' ' or '\t', ..] && line.ContainsAnyExcept(' ', '\t');

                if (hasControlCharacter || (isContinuation && !isFolded))
                    throw new ArgumentException("A header value must not contain control characters other than a tab, and may break a line only to continue it with whitespace.", paramName);

                if (lineBreakAt < 0)
                    return;

                remaining = remaining[(lineBreakAt + 1)..];
                isContinuation = true;
            }
        }
    }
}
