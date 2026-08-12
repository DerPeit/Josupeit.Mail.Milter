using System;
using System.Runtime.CompilerServices;

namespace Josupeit.Mail.Milter;

// Guards the strings a filter hands to the MTA. Each one ends up in a NUL-terminated field and, for
// headers and replies, in the delivered message, so a stray control character either cuts the field
// short or forges a line the sender never wrote.
internal static class WireText
{
    private const char FIRST_CONTROL_CHARACTER = (char)0;
    private const char LAST_CONTROL_CHARACTER = (char)31;

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
            if (name.AsSpan().ContainsAnyExceptInRange('!', '~') || name.Contains(':'))
                throw new ArgumentException("A header name must consist of printable ASCII characters other than a colon.", paramName);
        }
    }
}
