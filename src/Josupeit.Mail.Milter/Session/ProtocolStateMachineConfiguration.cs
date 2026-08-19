// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Session;

internal sealed record ProtocolStateMachineConfiguration(
    ProtocolRequirements ProtocolRequirements,
    ActionRequirements ActionRequirements,
    DeliveryRequirements DeliveryRequirements = DeliveryRequirements.None,
    NoReplyRequirements NoReplyRequirements = NoReplyRequirements.None)
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

        if (ActionRequirements.HasFlag(ActionRequirements.ChangeSender))
            flags |= ActionDiscriminator.ChangeSender;

        if (ActionRequirements.HasFlag(ActionRequirements.AddRecipientsWithParameters))
            flags |= ActionDiscriminator.AddRecipientsWithParameters;

        return flags;
    }

    // One flag per event, and no member for end-of-body: the MTA always waits for the verdict there, so
    // the protocol gives it no flag to set.
    private ContentBypassDiscriminator ToNoReplyFlags()
    {
        var flags = ContentBypassDiscriminator.None;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.Connect))
            flags |= ContentBypassDiscriminator.NoReplyToConnect;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.ClientHello))
            flags |= ContentBypassDiscriminator.NoReplyToClientHello;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.Sender))
            flags |= ContentBypassDiscriminator.NoReplyToSender;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.Recipient))
            flags |= ContentBypassDiscriminator.NoReplyToRecipient;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.MessageData))
            flags |= ContentBypassDiscriminator.NoReplyToMessageData;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.UnknownCommand))
            flags |= ContentBypassDiscriminator.NoReplyToUnknownCommand;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.Header))
            flags |= ContentBypassDiscriminator.NoReplyToHeader;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.EndOfHeaders))
            flags |= ContentBypassDiscriminator.NoReplyToEndOfHeaders;

        if (NoReplyRequirements.HasFlag(NoReplyRequirements.Body))
            flags |= ContentBypassDiscriminator.NoReplyToBody;

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

        // Positive flags, unlike everything else in this method: they ask the MTA for different
        // behaviour rather than for fewer events, so they are set when wanted rather than when not.
        if (DeliveryRequirements.HasFlag(DeliveryRequirements.LeadingHeaderSpace))
            flags |= ContentBypassDiscriminator.LeadingHeaderSpace;

        if (DeliveryRequirements.HasFlag(DeliveryRequirements.RejectedRecipients))
            flags |= ContentBypassDiscriminator.RejectedRecipients;

        // Also positive: naming an event here tells the MTA not to wait for an answer to it.
        flags |= ToNoReplyFlags();

        if (!IsAwaitingHeaders)
            flags |= ContentBypassDiscriminator.NoHeader;

        if (!IsAwaitingEndOfHeaders)
            flags |= ContentBypassDiscriminator.NoEndOfHeaders;

        if (!IsAwaitingBody)
            flags |= ContentBypassDiscriminator.NoBody;

        return flags;
    }
}