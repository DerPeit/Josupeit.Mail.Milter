// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Linq;
using System.Text;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

// SMFIF_SETSYMLIST trims the macros the MTA sends to the ones the filter reads. Two things are easy to
// get wrong and neither fails loudly: the stage codes are not ProtocolStage's own numbers, and the names
// for one stage travel as a single space-separated string.
[TestFixture]
public sealed class MacroRequirementsTests
{
    [TestCase(ProtocolStage.Connect, 0u)]
    [TestCase(ProtocolStage.ClientHello, 1u)]
    [TestCase(ProtocolStage.Sender, 2u)]
    [TestCase(ProtocolStage.Recipient, 3u)]
    [TestCase(ProtocolStage.MessageData, 4u)]
    [TestCase(ProtocolStage.EndOfBody, 5u)]
    [TestCase(ProtocolStage.EndOfHeaders, 6u)]
    public void A_stage_carries_the_macro_class_the_protocol_gives_it(ProtocolStage stage, uint expected)
    {
        Assert.That(MacroRequirements.GetMacroStageCode(stage), Is.EqualTo(expected));
    }

    // The two that would be wrong by a plain cast: conversation order runs Header then EndOfHeaders,
    // the macro classes run the other way round.
    [Test]
    public void The_macro_classes_are_not_the_conversation_order()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MacroRequirements.GetMacroStageCode(ProtocolStage.EndOfBody), Is.EqualTo(5u));
            Assert.That(MacroRequirements.GetMacroStageCode(ProtocolStage.EndOfHeaders), Is.EqualTo(6u));
            Assert.That((int)ProtocolStage.EndOfBody, Is.GreaterThan((int)ProtocolStage.EndOfHeaders));
        });
    }

    [TestCase(ProtocolStage.Header)]
    [TestCase(ProtocolStage.Body)]
    public void A_stage_with_no_macro_class_cannot_be_asked_for(ProtocolStage stage)
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => MacroRequirements.GetMacroStageCode(stage), Throws.ArgumentException);
            Assert.That(() => MacroRequirements.For(stage, "i"), Throws.ArgumentException);
        });
    }

    [Test]
    public void A_macro_name_containing_a_space_is_rejected()
    {
        Assert.That(
            () => MacroRequirements.For(ProtocolStage.Sender, "{mail_addr} {auth_authen}"),
            Throws.ArgumentException,
            "One stage's names travel as a single space-separated string, so a space splits one name into two.");
    }

    [Test]
    public void A_macro_name_carrying_a_control_character_is_rejected()
    {
        Assert.That(() => MacroRequirements.For(ProtocolStage.Sender, "{mail\r\naddr}"), Throws.ArgumentException);
    }

    [Test]
    public void Naming_no_macros_at_all_asks_for_no_list()
    {
        Assert.That(MacroRequirements.None.IsEmpty, Is.True);
    }

    // The regression guard for the frame growing a variable tail: a filter naming no macros must produce
    // the same twelve bytes it always did.
    [Test]
    public void A_negotiation_naming_no_macros_is_the_same_twelve_bytes_as_before()
    {
        using var frame = Negotiate(macroRequirements: null);

        Assert.That(GetData(frame), Has.Length.EqualTo(12));
    }

    [Test]
    public void Each_named_stage_is_appended_as_a_code_and_a_space_separated_list()
    {
        using var frame = Negotiate(MacroRequirements.For(ProtocolStage.Sender, "{mail_addr}", "{auth_authen}"));

        var expected = new byte[] { 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 }
            .Concat(Encoding.Latin1.GetBytes("{mail_addr} {auth_authen}\0"))
            .ToArray();

        Assert.That(GetData(frame), Is.EqualTo(expected));
    }

    // A dictionary has no defined enumeration order, and this frame is asserted on byte for byte.
    [Test]
    public void Stages_are_appended_in_ascending_macro_class_order()
    {
        var requirements = MacroRequirements
            .For(ProtocolStage.EndOfBody, "{msg_id}")
            .And(ProtocolStage.Connect, "j");

        using var frame = Negotiate(requirements);

        var expected = new byte[] { 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0 }
            .Concat(new byte[] { 0, 0, 0, 0 }).Concat(Encoding.Latin1.GetBytes("j\0"))
            .Concat(new byte[] { 0, 0, 0, 5 }).Concat(Encoding.Latin1.GetBytes("{msg_id}\0"))
            .ToArray();

        Assert.That(GetData(frame), Is.EqualTo(expected));
    }

    private static ResponseFrame Negotiate(MacroRequirements? macroRequirements)
    {
        var packet = new OptionNegotiationResponsePacket
        {
            MilterProtocolVersion = new Version(6, 0),
            MilterRequiredActions = ActionDiscriminator.None,
            MilterUndesiredProtocolContent = ContentBypassDiscriminator.None,
            MilterRequestedMacros = macroRequirements,
        };

        return new ResponseFrameFactory().CreateResponseFrame(packet);
    }

    private static byte[] GetData(ResponseFrame frame)
    {
        return frame.Memory is null ? [] : frame.Memory.Memory[frame.DataRange].ToArray();
    }
}
