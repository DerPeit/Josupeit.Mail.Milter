// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Link;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

// The All composites read as "everything of one kind", and both are used as a stand-in for a
// permissive MTA. A version 6 member folded into either would quietly change what that stand-in
// means - "send me nothing" would start implying "and expect no answer either" - so the membership
// is pinned here rather than left to whoever adds the next flag.
[TestFixture]
public sealed class NegotiationFlagTests
{
    [Test]
    public void Skippable_events_are_the_only_members_of_the_content_bypass_composite()
    {
        var skippableEvents = ContentBypassDiscriminator.NoConnect
                       | ContentBypassDiscriminator.NoHelo
                       | ContentBypassDiscriminator.NoSender
                       | ContentBypassDiscriminator.NoRecipient
                       | ContentBypassDiscriminator.NoBody
                       | ContentBypassDiscriminator.NoHeader
                       | ContentBypassDiscriminator.NoEndOfHeaders
                       | ContentBypassDiscriminator.NoUnknownCommand
                       | ContentBypassDiscriminator.NoMessageData;

        Assert.That(skippableEvents, Is.EqualTo(ContentBypassDiscriminator.All));
    }

    [Test]
    public void The_content_bypass_composite_asks_for_no_behaviour_beyond_skipping()
    {
        var behaviours = ContentBypassDiscriminator.SkipRemainingEvents
                         | ContentBypassDiscriminator.RejectedRecipients
                         | ContentBypassDiscriminator.LeadingHeaderSpace
                         | ContentBypassDiscriminator.NoReplyToConnect
                         | ContentBypassDiscriminator.NoReplyToClientHello
                         | ContentBypassDiscriminator.NoReplyToSender
                         | ContentBypassDiscriminator.NoReplyToRecipient
                         | ContentBypassDiscriminator.NoReplyToMessageData
                         | ContentBypassDiscriminator.NoReplyToUnknownCommand
                         | ContentBypassDiscriminator.NoReplyToHeader
                         | ContentBypassDiscriminator.NoReplyToEndOfHeaders
                         | ContentBypassDiscriminator.NoReplyToBody;

        Assert.That(ContentBypassDiscriminator.All & behaviours, Is.EqualTo(ContentBypassDiscriminator.None));
    }

    [Test]
    public void Modifications_are_the_only_members_of_the_action_composite()
    {
        var modifications = ActionDiscriminator.AddHeaders
                       | ActionDiscriminator.ChangeBodyChunks
                       | ActionDiscriminator.AddRecipients
                       | ActionDiscriminator.DeleteRecipients
                       | ActionDiscriminator.ChangeOrDeleteHeaders
                       | ActionDiscriminator.QuarantineMessage
                       | ActionDiscriminator.ChangeSender
                       | ActionDiscriminator.AddRecipientsWithParameters;

        Assert.That(modifications, Is.EqualTo(ActionDiscriminator.All));
    }

    [Test]
    public void Naming_the_wanted_macros_changes_no_message_and_stays_out_of_the_action_composite()
    {
        Assert.That(ActionDiscriminator.All.HasFlag(ActionDiscriminator.SetMacroList), Is.False);
    }

    [Test]
    public void The_no_reply_family_occupies_the_bits_the_protocol_gives_it()
    {
        Assert.Multiple(() =>
        {
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToHeader, Is.EqualTo(1u << 7));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToConnect, Is.EqualTo(1u << 12));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToClientHello, Is.EqualTo(1u << 13));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToSender, Is.EqualTo(1u << 14));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToRecipient, Is.EqualTo(1u << 15));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToMessageData, Is.EqualTo(1u << 16));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToUnknownCommand, Is.EqualTo(1u << 17));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToEndOfHeaders, Is.EqualTo(1u << 18));
            Assert.That((uint)ContentBypassDiscriminator.NoReplyToBody, Is.EqualTo(1u << 19));
        });
    }

    [Test]
    public void The_version_6_command_and_response_codes_carry_their_protocol_letters()
    {
        Assert.Multiple(() =>
        {
            Assert.That((byte)CommandCode.MessageData, Is.EqualTo((byte)'T'));
            Assert.That((byte)CommandCode.UnknownCommand, Is.EqualTo((byte)'U'));
            Assert.That((byte)CommandCode.QuitForNewConnection, Is.EqualTo((byte)'K'));
            Assert.That((byte)ResponseCode.InsertHeader, Is.EqualTo((byte)'i'));
            Assert.That((byte)ResponseCode.ChangeSender, Is.EqualTo((byte)'e'));
            Assert.That((byte)ResponseCode.AddRecipientWithParameters, Is.EqualTo((byte)'2'));
            Assert.That((byte)ResponseCode.SetMacroList, Is.EqualTo((byte)'l'));
            Assert.That((byte)ResponseCode.Skip, Is.EqualTo((byte)'s'));
        });
    }
}
