// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Josupeit.Mail.Milter.Link;
using Josupeit.Mail.Milter.Protocol;
using Josupeit.Mail.Milter.Session;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

// SMFIP_RCPT_REJ has no field in the RCPT frame. The MTA reports it in the macros it sends immediately
// before, so the adapter has to put the two back together - and then forget, because the next recipient
// may arrive with no macros of its own.
[TestFixture]
public sealed class RejectedRecipientTests
{
    [Test]
    public async Task A_recipient_the_mta_refused_is_marked_as_such()
    {
        var recipients = new List<RecipientInformation>();
        var adapter = new ContextAdapter(() => new RecipientRecordingContext(recipients), TestNegotiation.Nothing);

        await Drain(adapter.OnDefineMacro(RecipientMacros(mailer: "error")));
        await Drain(adapter.OnRecipient(Recipient("<nosuchbox@example.com>")));

        Assert.That(recipients[0].IsRejectedByMta, Is.True);
    }

    [Test]
    public async Task A_recipient_the_mta_accepted_is_not_marked()
    {
        var recipients = new List<RecipientInformation>();
        var adapter = new ContextAdapter(() => new RecipientRecordingContext(recipients), TestNegotiation.Nothing);

        await Drain(adapter.OnDefineMacro(RecipientMacros(mailer: "smtp")));
        await Drain(adapter.OnRecipient(Recipient("<pennypost@example.com>")));

        Assert.That(recipients[0].IsRejectedByMta, Is.False);
    }

    // The one that only shows up under load: a second RCPT TO can arrive without macros of its own, and
    // would otherwise inherit the verdict passed on the first.
    [Test]
    public async Task A_verdict_is_not_carried_over_to_the_next_recipient()
    {
        var recipients = new List<RecipientInformation>();
        var adapter = new ContextAdapter(() => new RecipientRecordingContext(recipients), TestNegotiation.Nothing);

        await Drain(adapter.OnDefineMacro(RecipientMacros(mailer: "error")));
        await Drain(adapter.OnRecipient(Recipient("<nosuchbox@example.com>")));
        await Drain(adapter.OnRecipient(Recipient("<realperson@example.com>")));

        Assert.Multiple(() =>
        {
            Assert.That(recipients[0].IsRejectedByMta, Is.True);
            Assert.That(recipients[1].IsRejectedByMta, Is.False, "The second recipient carried no macros of its own.");
        });
    }

    [Test]
    public async Task A_recipient_arriving_with_no_macros_at_all_is_not_marked()
    {
        var recipients = new List<RecipientInformation>();
        var adapter = new ContextAdapter(() => new RecipientRecordingContext(recipients), TestNegotiation.Nothing);

        await Drain(adapter.OnRecipient(Recipient("<pennypost@example.com>")));

        Assert.That(recipients[0].IsRejectedByMta, Is.False);
    }

    // The braces are part of the macro name as it arrives on the wire. Looking it up without them
    // silently never matches, and the feature would appear to work while never firing.
    [Test]
    public async Task The_macro_name_is_matched_with_its_braces()
    {
        var recipients = new List<RecipientInformation>();
        var adapter = new ContextAdapter(() => new RecipientRecordingContext(recipients), TestNegotiation.Nothing);

        var withoutBraces = new DefineMacroRequestPacket
        {
            CommandCode = CommandCode.Recipient,
            Macros = new Dictionary<string, string> { ["rcpt_mailer"] = "error" },
        };

        await Drain(adapter.OnDefineMacro(withoutBraces));
        await Drain(adapter.OnRecipient(Recipient("<nosuchbox@example.com>")));

        Assert.That(recipients[0].IsRejectedByMta, Is.False, "Only {rcpt_mailer} is the macro Postfix sends.");
    }

    [Test]
    public async Task Macros_from_another_stage_do_not_mark_a_recipient()
    {
        var recipients = new List<RecipientInformation>();
        var adapter = new ContextAdapter(() => new RecipientRecordingContext(recipients), TestNegotiation.Nothing);

        var senderStage = new DefineMacroRequestPacket
        {
            CommandCode = CommandCode.Sender,
            Macros = new Dictionary<string, string> { ["{rcpt_mailer}"] = "error" },
        };

        await Drain(adapter.OnDefineMacro(senderStage));
        await Drain(adapter.OnRecipient(Recipient("<pennypost@example.com>")));

        Assert.That(recipients[0].IsRejectedByMta, Is.False);
    }

    private static DefineMacroRequestPacket RecipientMacros(string mailer)
    {
        return new DefineMacroRequestPacket
        {
            CommandCode = CommandCode.Recipient,
            Macros = new Dictionary<string, string> { ["{rcpt_mailer}"] = mailer },
        };
    }

    private static RecipientRequestPacket Recipient(string address)
    {
        return new RecipientRequestPacket { Recipient = address, ESmtpArguments = [] };
    }

    private static async Task Drain(IAsyncEnumerable<ResponsePacket> responses)
    {
        await foreach (var _ in responses)
        {
        }
    }

    private sealed class RecipientRecordingContext(List<RecipientInformation> recipients) : IMilterCallbacks
    {
        public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken)
        {
            recipients.Add(info);

            return new(CommitActions.Continue);
        }
    }
}
