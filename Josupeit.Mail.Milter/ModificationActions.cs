using System;
using System.IO;

namespace Josupeit.Mail.Milter;

public static class ModificationActions
{
    public static AddRecipientAction AddRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);
        WireText.ThrowIfContainsControlCharacter(recipient);

        return new AddRecipientAction(recipient);
    }

    public static DeleteRecipientAction DeleteRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);
        WireText.ThrowIfContainsControlCharacter(recipient);

        return new DeleteRecipientAction(recipient);
    }

    public static AddHeaderAction AddHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        WireText.ThrowIfNotHeaderName(name);
        WireText.ThrowIfContainsControlCharacter(value);

        return new AddHeaderAction(name, value);
    }

    public static ChangeHeaderAction ChangeHeader(string name, string value, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        WireText.ThrowIfNotHeaderName(name);
        WireText.ThrowIfContainsControlCharacter(value);

        return new ChangeHeaderAction(name, value, index);
    }

    public static DeleteHeaderAction DeleteHeader(string name, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        WireText.ThrowIfNotHeaderName(name);

        return new DeleteHeaderAction(name, index);
    }

    /// <param name="leaveOpen">
    /// Keeps <paramref name="stream"/> open once the body has been sent. The default disposes it, because
    /// the usual case builds a stream for this one message and has no use for it afterwards.
    /// </param>
    public static ReplaceBodyAction ReplaceBody(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new ReplaceBodyAction(stream, leaveOpen);
    }

    public static QuarantineAction QuarantineMessage(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        WireText.ThrowIfContainsControlCharacter(reason);

        return new QuarantineAction(reason);
    }
}
