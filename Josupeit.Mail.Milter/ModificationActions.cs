using System;
using System.IO;

namespace Josupeit.Mail.Milter;

public static class ModificationActions
{
    public static AddRecipientAction AddRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);

        return new AddRecipientAction(recipient);
    }

    public static DeleteRecipientAction DeleteRecipient(string recipient)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipient);

        return new DeleteRecipientAction(recipient);
    }

    public static AddHeaderAction AddHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);

        return new AddHeaderAction(name, value);
    }

    public static ChangeHeaderAction ChangeHeader(string name, string value, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);

        return new ChangeHeaderAction(name, value, index);
    }

    public static DeleteHeaderAction DeleteHeader(string name, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);

        return new DeleteHeaderAction(name, index);
    }

    public static ReplaceBodyAction ReplaceBody(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new ReplaceBodyAction(stream);
    }

    public static QuarantineAction QuarantineMessage(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);

        return new QuarantineAction(reason);
    }
}