// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class ActionValidationTests
{
    [TestCase("plain\r\nX-Injected: yes")]
    [TestCase("plain\nX-Injected: yes")]
    [TestCase("plain\0truncated")]
    [TestCase("plain\ttabbed")]
    public void A_header_value_carrying_a_control_character_is_rejected(string value)
    {
        Assert.That(() => ModificationActions.AddHeader("X-Test", value), Throws.ArgumentException);
    }

    [TestCase("X-Test: injected")]
    [TestCase("X Test")]
    [TestCase("X-Test\r\n")]
    public void A_malformed_header_name_is_rejected(string name)
    {
        Assert.That(() => ModificationActions.AddHeader(name, "value"), Throws.ArgumentException);
        Assert.That(() => ModificationActions.ChangeHeader(name, "value"), Throws.ArgumentException);
        Assert.That(() => ModificationActions.DeleteHeader(name), Throws.ArgumentException);
    }

    [TestCase("X-Test: injected")]
    [TestCase("X Test")]
    [TestCase("X-Test\r\n")]
    public void A_malformed_header_name_is_rejected_when_inserting(string name)
    {
        Assert.That(() => ModificationActions.InsertHeader(0, name, "value"), Throws.ArgumentException);
    }

    [Test]
    public void An_inserted_header_value_carrying_a_control_character_is_rejected()
    {
        Assert.That(
            () => ModificationActions.InsertHeader(0, "X-Test", "plain\r\nX-Injected: yes"),
            Throws.ArgumentException);
    }

    [Test]
    public void A_negative_insertion_index_is_rejected()
    {
        Assert.That(
            () => ModificationActions.InsertHeader(-1, "X-Test", "value"),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void An_inserted_header_defaults_to_the_top_of_the_message()
    {
        var action = ModificationActions.InsertHeader(0, "X-Checked-By", "Sybil Sieve");

        Assert.That(action.Index, Is.Zero);
    }

    [Test]
    public void A_sender_carrying_a_control_character_is_rejected()
    {
        Assert.That(
            () => ModificationActions.ChangeSender("<eve@example.com>\r\nMAIL FROM:<mallory@example.com>"),
            Throws.ArgumentException);
    }

    [Test]
    public void Sender_arguments_carrying_a_control_character_are_rejected()
    {
        Assert.That(
            () => ModificationActions.ChangeSender("<bounces@example.com>", "BODY=8BITMIME\r\nDATA"),
            Throws.ArgumentException);
    }

    [Test]
    public void A_sender_without_arguments_is_accepted()
    {
        var action = ModificationActions.ChangeSender("<bounces@example.com>");

        Assert.That(action.ESmtpArguments, Is.Null);
    }

    [TestCase("<archive@example.com>\r\nRCPT TO:<eve@example.com>", "NOTIFY=NEVER")]
    [TestCase("<archive@example.com>", "NOTIFY=NEVER\r\nDATA")]
    public void A_parameterised_recipient_carrying_a_control_character_is_rejected(string recipient, string arguments)
    {
        Assert.That(
            () => ModificationActions.AddRecipient(recipient, arguments),
            Throws.ArgumentException);
    }

    [Test]
    public void A_parameterised_recipient_requires_its_parameters()
    {
        Assert.That(
            () => ModificationActions.AddRecipient("<archive@example.com>", string.Empty),
            Throws.ArgumentException);
    }

    [Test]
    public void A_header_value_outside_ASCII_is_accepted()
    {
        var action = ModificationActions.AddHeader("X-Test", "Grüße");

        Assert.That(action.Value, Is.EqualTo("Grüße"));
    }

    [Test]
    public void A_reply_text_carrying_a_control_character_is_rejected()
    {
        Assert.That(() => CommitActions.Reply(550, "denied\r\n250 ok"), Throws.ArgumentException);
    }

    [Test]
    public void A_recipient_carrying_a_control_character_is_rejected()
    {
        Assert.That(() => ModificationActions.AddRecipient("bob@example.com\r\nRCPT TO:<eve@example.com>"), Throws.ArgumentException);
        Assert.That(() => ModificationActions.DeleteRecipient("bob@example.com\0"), Throws.ArgumentException);
    }

    [Test]
    public void A_quarantine_reason_carrying_a_control_character_is_rejected()
    {
        Assert.That(() => ModificationActions.QuarantineMessage("spam\r\nheld"), Throws.ArgumentException);
    }

    // Validation is only worth anything if it cannot be walked around, so no action may offer a public
    // constructor. The `with` expression is closed off too, because the properties are get-only.
    [Test]
    public void No_action_can_be_built_without_going_through_a_factory()
    {
        var actionTypes = typeof(Action).Assembly.GetExportedTypes()
            .Where(type => typeof(Action).IsAssignableFrom(type) && !type.IsAbstract);

        var publiclyConstructible = actionTypes
            .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .ToArray();

        Assert.That(publiclyConstructible, Is.Empty);
    }

    [Test]
    public void The_rejected_argument_is_named()
    {
        Assert.That(
            () => ModificationActions.AddHeader("X-Test", "bad\r\n"),
            Throws.ArgumentException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("value"));
    }
}
