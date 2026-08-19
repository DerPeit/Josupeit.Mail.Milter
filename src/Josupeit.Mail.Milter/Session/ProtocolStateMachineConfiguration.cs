// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Session;

internal sealed record ProtocolStateMachineConfiguration(ProtocolRequirements ProtocolRequirements, ActionRequirements ActionRequirements)
{
    public bool IsAwaitingConnection => ProtocolRequirements.HasFlag(ProtocolRequirements.Connect);

    public bool IsAwaitingClientHello => ProtocolRequirements.HasFlag(ProtocolRequirements.ClientHello);

    public bool IsAwaitingSender => ProtocolRequirements.HasFlag(ProtocolRequirements.Sender);

    public bool IsAwaitingRecipients => ProtocolRequirements.HasFlag(ProtocolRequirements.Recipient);

    public bool IsAwaitingMessageData => ProtocolRequirements.HasFlag(ProtocolRequirements.MessageData);

    public bool IsAwaitingUnknownCommands => ProtocolRequirements.HasFlag(ProtocolRequirements.UnknownCommand);

    public bool IsAwaitingHeaders => ProtocolRequirements.HasFlag(ProtocolRequirements.Header);

    public bool IsAwaitingEndOfHeaders => ProtocolRequirements.HasFlag(ProtocolRequirements.EndOfHeaders);

    public bool IsAwaitingBody => ProtocolRequirements.HasFlag(ProtocolRequirements.Body);

    internal ActionDiscriminator ToActionDiscriminator()
    {
        var flags = ActionDiscriminator.None;

        if (ActionRequirements.HasFlag(ActionRequirements.AddRecipients))
            flags |= ActionDiscriminator.AddRecipients;

        if (ActionRequirements.HasFlag(ActionRequirements.DeleteRecipients))
            flags |= ActionDiscriminator.DeleteRecipients;

        if (ActionRequirements.HasFlag(ActionRequirements.AddHeaders))
            flags |= ActionDiscriminator.AddHeaders;

        if (ActionRequirements.HasFlag(ActionRequirements.ChangeHeaders))
            flags |= ActionDiscriminator.ChangeOrDeleteHeaders;

        if (ActionRequirements.HasFlag(ActionRequirements.DeleteHeaders))
            flags |= ActionDiscriminator.ChangeOrDeleteHeaders;

        if (ActionRequirements.HasFlag(ActionRequirements.ChangeBody))
            flags |= ActionDiscriminator.ChangeBodyChunks;

        if (ActionRequirements.HasFlag(ActionRequirements.QuarantineMessage))
            flags |= ActionDiscriminator.QuarantineMessage;

        return flags;
    }

    internal ContentBypassDiscriminator ToContentBypassDiscriminator()
    {
        var flags = ContentBypassDiscriminator.None;

        if (!IsAwaitingConnection)
            flags |= ContentBypassDiscriminator.NoConnect;

        if (!IsAwaitingClientHello)
            flags |= ContentBypassDiscriminator.NoHelo;

        if (!IsAwaitingSender)
            flags |= ContentBypassDiscriminator.NoSender;

        if (!IsAwaitingRecipients)
            flags |= ContentBypassDiscriminator.NoRecipient;

        if (!IsAwaitingMessageData)
            flags |= ContentBypassDiscriminator.NoMessageData;

        if (!IsAwaitingUnknownCommands)
            flags |= ContentBypassDiscriminator.NoUnknownCommand;

        if (!IsAwaitingHeaders)
            flags |= ContentBypassDiscriminator.NoHeader;

        if (!IsAwaitingEndOfHeaders)
            flags |= ContentBypassDiscriminator.NoEndOfHeaders;

        if (!IsAwaitingBody)
            flags |= ContentBypassDiscriminator.NoBody;

        return flags;
    }
}