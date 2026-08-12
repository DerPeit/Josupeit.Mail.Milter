using System;
using System.IO;

namespace Josupeit.Mail.Milter;

public static class ModificationActions
{
    public static AddRecipientAction AddRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);
        ArgumentException.ThrowIfContainsControlCharacter(recipient);

        return new AddRecipientAction(recipient);
    }

    public static DeleteRecipientAction DeleteRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);
        ArgumentException.ThrowIfContainsControlCharacter(recipient);

        return new DeleteRecipientAction(recipient);
    }

    public static AddHeaderAction AddHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNotHeaderName(name);
        ArgumentException.ThrowIfContainsControlCharacter(value);

        return new AddHeaderAction(name, value);
    }

    public static ChangeHeaderAction ChangeHeader(string name, string value, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentException.ThrowIfNotHeaderName(name);
        ArgumentException.ThrowIfContainsControlCharacter(value);

        return new ChangeHeaderAction(name, value, index);
    }

    public static DeleteHeaderAction DeleteHeader(string name, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        ArgumentException.ThrowIfNotHeaderName(name);

        return new DeleteHeaderAction(name, index);
    }

    public static ReplaceBodyAction ReplaceBody(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new ReplaceBodyAction(stream, leaveOpen);
    }

    public static QuarantineAction QuarantineMessage(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ArgumentException.ThrowIfContainsControlCharacter(reason);

        return new QuarantineAction(reason);
    }
}
