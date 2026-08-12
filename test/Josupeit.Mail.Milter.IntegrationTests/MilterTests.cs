// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using NSubstitute;

namespace Josupeit.Mail.Milter.IntegrationTests;

[TestOf(typeof(Milter<>)), CancelAfter(10_000)]
public class MilterTests : MilterFixture
{
    // A body long enough to cross Postfix's MILTER_CHUNK_SIZE, so it arrives as several SMFIC_BODY frames
    // and the body test covers reassembly rather than a single chunk.
    private static readonly string _multiChunkBody = LoadMultiChunkBody();

    [Test]
    public async Task Minimum_test_setup_works()
    {
        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        _ = ContextAdapter.Received().OnEndOfBody();
    }

    [Test]
    public async Task Exactly_one_context_adapter_is_being_created()
    {
        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        _ = ContextAdapterResolver.Received(1)();
    }

    [Test, CancelAfter(30_000)]
    public async Task A_second_message_on_the_same_connection_gets_its_own_context()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.All;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        // Two messages inside one SMTP conversation. The body of each is sent as a single write, because
        // Postfix answers nothing between DATA and the terminating dot.
        await Converse(
            "EHLO test.example.com",
            "MAIL FROM:<random@example.com>",
            "RCPT TO:<root@localhost.localdomain>",
            "DATA",
            "Subject: First\r\n\r\nFirst body\r\n.",
            "MAIL FROM:<random@example.com>",
            "RCPT TO:<root@localhost.localdomain>",
            "DATA",
            "Subject: Second\r\n\r\nSecond body\r\n.",
            "QUIT");

        var firstDelivery = await ReceiveMail();
        var secondDelivery = await ReceiveMail();

        Assert.That(
            firstDelivery + secondDelivery,
            Does.Contain("Subject: First").And.Contains("Subject: Second"));

        // One context per message, even though the connection was never re-opened.
        _ = ContextAdapterResolver.Received(2)();
        _ = ContextAdapter.Received(2).OnEndOfBody();

        // Postfix sends SMFIC_CONNECT once per connection, so the second message only sees a connect event
        // because the session replays the recorded one into the new context. HELO is not recorded, which is
        // what makes these two counts differ - and the difference is the whole point of the replay states.
        _ = ContextAdapter.ReceivedWithAnyArgs(2).OnConnectionEstablished(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs(1).OnClientHello(default, default);
    }

    [Test, CancelAfter(30_000)]
    public async Task A_reset_abandons_the_transaction_and_the_next_one_still_works()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.All;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        // RSET part way through a transaction is what makes Postfix send SMFIC_ABORT, which is the only
        // way into the aborted states.
        await Converse(
            "EHLO test.example.com",
            "MAIL FROM:<random@example.com>",
            "RCPT TO:<root@localhost.localdomain>",
            "RSET",
            "MAIL FROM:<random@example.com>",
            "RCPT TO:<root@localhost.localdomain>",
            "DATA",
            "Subject: After reset\r\n\r\nBody\r\n.",
            "QUIT");

        Assert.That(await ReceiveMail(), Does.Contain("Subject: After reset"));

        // The abandoned transaction never reached end of body, though both transactions reached the filter.
        _ = ContextAdapter.Received(1).OnEndOfBody();
        _ = ContextAdapter.ReceivedWithAnyArgs(2).OnSender(default, default);

        // The abort threw the first context away, so the second transaction had the connect event replayed
        // into a fresh one. The number of contexts created is deliberately not asserted: Postfix sends
        // macros ahead of every command, and a macro on its own is enough to build one, so the count
        // follows its macro cadence rather than anything this test controls.
        _ = ContextAdapter.ReceivedWithAnyArgs(2).OnConnectionEstablished(default, default);
    }

    [Test]
    public async Task All_callbacks_are_being_called()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.All;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        _ = ContextAdapter.ReceivedWithAnyArgs().OnDefineMacro(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnConnectionEstablished(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnClientHello(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnSender(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnRecipient(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnHeader(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnEndOfHeaders(default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnBodyChunk(default, default);
        _ = ContextAdapter.ReceivedWithAnyArgs().OnEndOfBody();
    }

    [Test]
    [TestCase(ProtocolRequirements.All ^ ProtocolRequirements.Connect)]
    [TestCase(ProtocolRequirements.All ^ ProtocolRequirements.ClientHello)]
    [TestCase(ProtocolRequirements.All ^ ProtocolRequirements.Sender)]
    [TestCase(ProtocolRequirements.All ^ ProtocolRequirements.Recipient)]
    [TestCase(ProtocolRequirements.All ^ ProtocolRequirements.Header)]
    [TestCase(ProtocolRequirements.All ^ ProtocolRequirements.EndOfHeaders)]
    [TestCase(ProtocolRequirements.All ^ ProtocolRequirements.Body)]
    public async Task Callbacks_that_are_not_required_are_not_being_called(ProtocolRequirements protocolRequirements)
    {
        MilterContextAdapter.ProtocolRequirements = protocolRequirements;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        if (!protocolRequirements.HasFlag(ProtocolRequirements.Connect))
            _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnConnectionEstablished(default, default);

        if (!protocolRequirements.HasFlag(ProtocolRequirements.ClientHello))
            _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnClientHello(default, default);

        if (!protocolRequirements.HasFlag(ProtocolRequirements.Sender))
            _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnSender(default, default);

        if (!protocolRequirements.HasFlag(ProtocolRequirements.Recipient))
            _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnRecipient(default, default);

        if (!protocolRequirements.HasFlag(ProtocolRequirements.Header))
            _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnHeader(default, default);

        if (!protocolRequirements.HasFlag(ProtocolRequirements.EndOfHeaders))
            _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnEndOfHeaders(default);

        if (!protocolRequirements.HasFlag(ProtocolRequirements.Body))
            _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnBodyChunk(default, default);
    }

    [Test]
    public async Task Milter_receives_correct_connection_information()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Connect;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        ConnectionInformation? observedInfo = null;

        _ = ContextAdapter.OnConnectionEstablished(Arg.Do<ConnectionInformation>(ci => observedInfo = ci), Arg.Any<CancellationToken>());

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        Assert.That(observedInfo, Is.Not.Null);

        // We cannot really say which information is correct, so we're happy to assert non-nullability
        Assert.That(observedInfo.Value.EndPoint, Is.InstanceOf<IPEndPoint>());
        Assert.That(observedInfo.Value.EndPoint!.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork));
        Assert.That(observedInfo.Value.Hostname, Is.Not.Empty);
        Assert.That(((IPEndPoint)observedInfo.Value.EndPoint).Port, Is.Not.EqualTo(0));
    }

    // Postfix never reports SMFIA_UNIX ('L'): a real unix socket peer is downgraded to AF_UNSPEC by
    // smtpd_peer_sockaddr_to_hostaddr(), which arrives here as SMFIA_UNKNOWN. Only sendmail emits 'L',
    // so that branch of the connect parser cannot be covered from this fixture.
    [Test]
    public async Task Milter_receives_connection_information_for_unknown_family()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Connect;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        ConnectionInformation? observedInfo = null;

        _ = ContextAdapter.OnConnectionEstablished(Arg.Do<ConnectionInformation>(ci => observedInfo = ci), Arg.Any<CancellationToken>());

        // Stand-alone smtpd reads SMTP from a pipe, so Postfix cannot determine a peer address and
        // reports SMFIA_UNKNOWN. It must run as the postfix user: SMTPD_STAND_ALONE() skips milter
        // setup entirely for any other user, and the milter would never be called.
        await SubmitMailLocally(
            "printf 'EHLO test\\r\\nMAIL FROM:<random@example.com>\\r\\nRCPT TO:<root@localhost.localdomain>\\r\\n"
            + "DATA\\r\\nSubject: Subject\\r\\n\\r\\nBody\\r\\n.\\r\\nQUIT\\r\\n'"
            + " | runuser -u postfix -- /usr/sbin/sendmail -bs");

        await ReceiveMail();

        Assert.That(observedInfo, Is.Not.Null);

        // SMFIA_UNKNOWN carries neither a port nor an address, and no EndPoint expresses "there is no
        // address", so the filter sees null.
        Assert.That(observedInfo.Value.EndPoint, Is.Null);
        Assert.That(observedInfo.Value.Hostname, Is.Not.Empty);
    }

    [Test]
    public async Task Milter_receives_connection_information_for_unavailable_address()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Connect;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        ConnectionInformation? observedInfo = null;

        _ = ContextAdapter.OnConnectionEstablished(Arg.Do<ConnectionInformation>(ci => observedInfo = ci), Arg.Any<CancellationToken>());

        // XCLIENT leaves the address family at AF_INET but replaces the address with the literal
        // "unknown", and it restarts the session, so the milter sees a second connect event.
        await Converse("EHLO test", "XCLIENT ADDR=[UNAVAILABLE] NAME=[UNAVAILABLE]", "QUIT");

        Assert.That(observedInfo, Is.Not.Null);

        // The family stays AF_INET but the address is the literal "unknown", which is the MTA saying it
        // has no address. That is reported the same way an unknown family is, so the family itself is
        // not observable here.
        Assert.That(observedInfo.Value.EndPoint, Is.Null);
    }

    [Test, CancelAfter(30_000)]
    public async Task Milter_receives_connection_information_for_local_submission()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Connect;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        ConnectionInformation? observedInfo = null;

        _ = ContextAdapter.OnConnectionEstablished(Arg.Do<ConnectionInformation>(ci => observedInfo = ci), Arg.Any<CancellationToken>());

        // Locally submitted mail reaches the milter through cleanup and non_smtpd_milters. Postfix
        // substitutes a loopback client for it, whose address family depends on inet_protocols.
        await SubmitMailLocally("printf 'To: root@localhost.localdomain\\nSubject: Subject\\n\\nBody\\n' | /usr/sbin/sendmail -t");

        await ReceiveMail();

        Assert.That(observedInfo, Is.Not.Null);

        Assert.That(observedInfo.Value.EndPoint, Is.InstanceOf<IPEndPoint>());
        Assert.That(((IPEndPoint)observedInfo.Value.EndPoint!).Address, Is.Not.EqualTo(IPAddress.None));
        Assert.That(((IPEndPoint)observedInfo.Value.EndPoint!).Port, Is.EqualTo(0));
    }

    [Test]
    public async Task Milter_receives_correct_client_hello_information()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.ClientHello;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        ClientHelloInformation? observedInfo = null;

        _ = ContextAdapter.OnClientHello(Arg.Do<ClientHelloInformation>(ci => observedInfo = ci), Arg.Any<CancellationToken>());

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        Assert.That(observedInfo, Is.Not.Null);
        Assert.That(observedInfo.Value.Hostname, Is.Not.Empty);
    }

    [Test]
    public async Task Milter_receives_correct_sender_information()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Sender;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        SenderRecipientInformation? observedInfo = null;

        _ = ContextAdapter.OnSender(Arg.Do<SenderRecipientInformation>(ci => observedInfo = ci), Arg.Any<CancellationToken>());

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        Assert.That(observedInfo, Is.Not.Null);
        Assert.That(observedInfo.Value.AddressString, Is.EqualTo("<random@example.com>"));
    }

    [Test]
    public async Task Milter_receives_correct_recipient_information()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Recipient;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        SenderRecipientInformation? observedInfo = null;

        _ = ContextAdapter.OnRecipient(Arg.Do<SenderRecipientInformation>(ci => observedInfo = ci), Arg.Any<CancellationToken>());

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        Assert.That(observedInfo, Is.Not.Null);
        Assert.That(observedInfo.Value.AddressString, Is.EqualTo("<root@localhost.localdomain>"));
    }

    [Test]
    public async Task Milter_receives_correct_header_information()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Header;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        Dictionary<string, string> observedInfo = [];

        _ = ContextAdapter.OnHeader(Arg.Do<HeaderInformation>(hi => observedInfo.Add(hi.Name, hi.Value)), Arg.Any<CancellationToken>());

        var message = new MailMessage
        {
            Headers =
            {
                { "Message-ID", "94683E7A-4D53-4305-9ADC-E52D8865EE4E" },
            },
            From = new MailAddress("random@example.com", "Random Example Guy"),
            To =
            {
                new MailAddress("root@localhost.localdomain", "Receiving Root Guy"),
            },
            Subject = "Hello world 😀",
            SubjectEncoding = Encoding.UTF8,
        };

        await SendMail(message);
        await ReceiveMail();

        Assert.That(observedInfo, Is.Not.Empty);
        Assert.That(observedInfo, Contains.Key("Message-ID").WithValue("94683E7A-4D53-4305-9ADC-E52D8865EE4E"));
        Assert.That(observedInfo, Contains.Key("Date"));
        Assert.That(observedInfo, Contains.Key("From").WithValue("\"Random Example Guy\" <random@example.com>"));
        Assert.That(observedInfo, Contains.Key("To").WithValue("\"Receiving Root Guy\" <root@localhost.localdomain>"));
        Assert.That(observedInfo, Contains.Key("Subject").WithValue("=?utf-8?B?SGVsbG8gd29ybGQg8J+YgA==?="));
    }

    [Test]
    public async Task Milter_receives_correct_body()
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.Body;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        await using var bodyStream = new MemoryStream();

        _ = ContextAdapter
            .OnBodyChunk(null, default)
            .ReturnsForAnyArgs<ValueTask<CommitAction>>(async ci =>
            {
                // ReSharper disable once AccessToDisposedClosure
                await bodyStream.WriteAsync(ci.Arg<byte[]>());

                return CommitActions.Continue;
            });

        var message = new MailMessage
        {
            Headers =
            {
                { "Message-ID", "94683E7A-4D53-4305-9ADC-E52D8865EE4E" },
            },
            From = new MailAddress("random@example.com", "Random Example Guy"),
            To =
            {
                new MailAddress("root@localhost.localdomain", "Receiving Root Guy"),
            },
            Subject = "Hello world 😀",
            SubjectEncoding = Encoding.UTF8,
            Body = _multiChunkBody,
            BodyEncoding = Encoding.ASCII,
        };

        await SendMail(message);
        await ReceiveMail();

        Assert.That(bodyStream.Length, Is.Not.Zero);

        bodyStream.Seek(0, 0);

        var body = message.BodyEncoding.GetString(bodyStream.ToArray());

        Assert.That(
            body.Trim().Replace("=0D", "\r").Replace("=0A", "\n").Replace("=\r\n", ""),
            Is.EqualTo(message.Body));
    }

    [Test]
    [TestCase(ProtocolRequirements.Connect)]
    [TestCase(ProtocolRequirements.ClientHello)]
    [TestCase(ProtocolRequirements.Sender)]
    [TestCase(ProtocolRequirements.Recipient)]
    [TestCase(ProtocolRequirements.Header)]
    [TestCase(ProtocolRequirements.EndOfHeaders)]
    [TestCase(ProtocolRequirements.Body)]
    public async Task Accepting_messages_quits_prematurely(ProtocolRequirements protocolRequirements)
    {
        MilterContextAdapter.ProtocolRequirements = protocolRequirements;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        var acceptPromise = new ValueTask<CommitAction>(CommitActions.Accept);

        ContextAdapter.OnConnectionEstablished(default, default).ReturnsForAnyArgs(acceptPromise);
        ContextAdapter.OnClientHello(default, default).ReturnsForAnyArgs(acceptPromise);
        ContextAdapter.OnSender(default, default).ReturnsForAnyArgs(acceptPromise);
        ContextAdapter.OnRecipient(default, default).ReturnsForAnyArgs(acceptPromise);
        ContextAdapter.OnHeader(default, default).ReturnsForAnyArgs(acceptPromise);
        ContextAdapter.OnEndOfHeaders(default).ReturnsForAnyArgs(acceptPromise);
        ContextAdapter.OnBodyChunk(default, default).ReturnsForAnyArgs(acceptPromise);

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));
        await ReceiveMail();

        _ = ContextAdapter.DidNotReceiveWithAnyArgs().OnEndOfBody();
    }

    [Test]
    [TestCase(ProtocolRequirements.Connect)]
    [TestCase(ProtocolRequirements.ClientHello)]
    [TestCase(ProtocolRequirements.Sender)]
    [TestCase(ProtocolRequirements.Recipient)]
    [TestCase(ProtocolRequirements.Header)]
    [TestCase(ProtocolRequirements.EndOfHeaders)]
    [TestCase(ProtocolRequirements.Body)]
    [TestCase(ProtocolRequirements.EndOfBody)]
    public async Task Deferring_message_works_as_expected(ProtocolRequirements protocolRequirements)
    {
        MilterContextAdapter.ProtocolRequirements = protocolRequirements;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        var deferPromise = new ValueTask<CommitAction>(CommitActions.Defer);

        ContextAdapter.OnConnectionEstablished(default, default).ReturnsForAnyArgs(deferPromise);
        ContextAdapter.OnClientHello(default, default).ReturnsForAnyArgs(deferPromise);
        ContextAdapter.OnSender(default, default).ReturnsForAnyArgs(deferPromise);
        ContextAdapter.OnRecipient(default, default).ReturnsForAnyArgs(deferPromise);
        ContextAdapter.OnHeader(default, default).ReturnsForAnyArgs(deferPromise);
        ContextAdapter.OnEndOfHeaders(default).ReturnsForAnyArgs(deferPromise);
        ContextAdapter.OnBodyChunk(default, default).ReturnsForAnyArgs(deferPromise);
        ContextAdapter.OnEndOfBody().ReturnsForAnyArgs(new[] { deferPromise.Result }.ToAsyncEnumerable());

        await Assert.ThatAsync(
            async () => await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body")),
            Throws.InstanceOf<SmtpException>().With.Message.Contains("4.7.1"));

        AssertDecidingCallbackRan(protocolRequirements);
    }

    [Test]
    [TestCase(ProtocolRequirements.Connect)]
    [TestCase(ProtocolRequirements.ClientHello)]
    [TestCase(ProtocolRequirements.Sender)]
    [TestCase(ProtocolRequirements.Recipient)]
    [TestCase(ProtocolRequirements.Header)]
    [TestCase(ProtocolRequirements.EndOfHeaders)]
    [TestCase(ProtocolRequirements.Body)]
    [TestCase(ProtocolRequirements.EndOfBody)]
    public async Task Rejecting_message_works_as_expected(ProtocolRequirements protocolRequirements)
    {
        MilterContextAdapter.ProtocolRequirements = protocolRequirements;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        var rejectPromise = new ValueTask<CommitAction>(CommitActions.Reject);

        ContextAdapter.OnConnectionEstablished(default, default).ReturnsForAnyArgs(rejectPromise);
        ContextAdapter.OnClientHello(default, default).ReturnsForAnyArgs(rejectPromise);
        ContextAdapter.OnSender(default, default).ReturnsForAnyArgs(rejectPromise);
        ContextAdapter.OnRecipient(default, default).ReturnsForAnyArgs(rejectPromise);
        ContextAdapter.OnHeader(default, default).ReturnsForAnyArgs(rejectPromise);
        ContextAdapter.OnEndOfHeaders(default).ReturnsForAnyArgs(rejectPromise);
        ContextAdapter.OnBodyChunk(default, default).ReturnsForAnyArgs(rejectPromise);
        ContextAdapter.OnEndOfBody().ReturnsForAnyArgs(new[] { rejectPromise.Result }.ToAsyncEnumerable());

        await Assert.ThatAsync(
            async () => await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body")),
            Throws.InstanceOf<SmtpException>().With.Message.Contains("5.7.1").Or.Message.Contains("not accepting connections"));
    }

    [Test]
    [TestCase(ProtocolRequirements.Connect)]
    [TestCase(ProtocolRequirements.ClientHello)]
    [TestCase(ProtocolRequirements.Sender)]
    [TestCase(ProtocolRequirements.Recipient)]
    [TestCase(ProtocolRequirements.Header)]
    [TestCase(ProtocolRequirements.EndOfHeaders)]
    [TestCase(ProtocolRequirements.Body)]
    [TestCase(ProtocolRequirements.EndOfBody)]
    public async Task Replying_to_message_works_as_expected(ProtocolRequirements protocolRequirements)
    {
        MilterContextAdapter.ProtocolRequirements = protocolRequirements;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        var replyPromise = new ValueTask<CommitAction>(CommitActions.Reply(450, "Go away for heavens sake"));

        ContextAdapter.OnConnectionEstablished(default, default).ReturnsForAnyArgs(replyPromise);
        ContextAdapter.OnClientHello(default, default).ReturnsForAnyArgs(replyPromise);
        ContextAdapter.OnSender(default, default).ReturnsForAnyArgs(replyPromise);
        ContextAdapter.OnRecipient(default, default).ReturnsForAnyArgs(replyPromise);
        ContextAdapter.OnHeader(default, default).ReturnsForAnyArgs(replyPromise);
        ContextAdapter.OnEndOfHeaders(default).ReturnsForAnyArgs(replyPromise);
        ContextAdapter.OnBodyChunk(default, default).ReturnsForAnyArgs(replyPromise);
        ContextAdapter.OnEndOfBody().ReturnsForAnyArgs(new[] { replyPromise.Result }.ToAsyncEnumerable());

        await Assert.ThatAsync(
            async () => await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body")),
            Throws.InstanceOf<SmtpException>().With.Message.Contains("Go away for heavens sake"));
    }

    [Test, CancelAfter(30_000)]
    [TestCase(ProtocolRequirements.Connect)]
    [TestCase(ProtocolRequirements.ClientHello)]
    [TestCase(ProtocolRequirements.Sender)]
    [TestCase(ProtocolRequirements.Recipient)]
    [TestCase(ProtocolRequirements.Header)]
    [TestCase(ProtocolRequirements.EndOfHeaders)]
    [TestCase(ProtocolRequirements.Body)]
    [TestCase(ProtocolRequirements.EndOfBody)]
    public async Task Discarding_message_works_as_expected(ProtocolRequirements protocolRequirements)
    {
        MilterContextAdapter.ProtocolRequirements = protocolRequirements;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        var discardPromise = new ValueTask<CommitAction>(CommitActions.Discard);

        ContextAdapter.OnConnectionEstablished(default, default).ReturnsForAnyArgs(discardPromise);
        ContextAdapter.OnClientHello(default, default).ReturnsForAnyArgs(discardPromise);
        ContextAdapter.OnSender(default, default).ReturnsForAnyArgs(discardPromise);
        ContextAdapter.OnRecipient(default, default).ReturnsForAnyArgs(discardPromise);
        ContextAdapter.OnHeader(default, default).ReturnsForAnyArgs(discardPromise);
        ContextAdapter.OnEndOfHeaders(default).ReturnsForAnyArgs(discardPromise);
        ContextAdapter.OnBodyChunk(default, default).ReturnsForAnyArgs(discardPromise);
        ContextAdapter.OnEndOfBody().ReturnsForAnyArgs(new[] { discardPromise.Result }.ToAsyncEnumerable());

        await Assert.ThatAsync(
            async () => await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body")),
            Throws.Nothing);

        await Assert.ThatAsync(
            async () => await ReceiveMail().WaitAsync(TimeSpan.FromSeconds(20)),
            Throws.InstanceOf<TimeoutException>());
    }

    [Test]
    public async Task Adding_a_header_reaches_the_delivered_mail()
    {
        RespondAtEndOfBody(
            ActionRequirements.AddHeaders,
            ModificationActions.AddHeader("X-Filtered-By", "Josupeit.Mail.Milter"));

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));

        Assert.That(await ReceiveMail(), Does.Contain("X-Filtered-By: Josupeit.Mail.Milter"));
    }

    [Test]
    public async Task Changing_a_header_reaches_the_delivered_mail()
    {
        RespondAtEndOfBody(
            ActionRequirements.ChangeHeaders,
            ModificationActions.ChangeHeader("Subject", "Rewritten by the filter"));

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Original subject", "Body"));

        var delivered = await ReceiveMail();

        Assert.That(delivered, Does.Contain("Subject: Rewritten by the filter"));
        Assert.That(delivered, Does.Not.Contain("Original subject"));
    }

    // The end-to-end proof that the header index is translated for the wire.
    //
    // A default index cannot show this: SMFIR_CHGHEADER counts occurrences from one, and Postfix quietly
    // treats the zero a naive implementation sends as a one, so changing the only occurrence works either
    // way. It is the second occurrence that separates them - untranslated, index 1 rewrites the first.
    [Test, CancelAfter(30_000)]
    public async Task Changing_the_second_occurrence_of_a_header_leaves_the_first_alone()
    {
        RespondAtEndOfBody(
            ActionRequirements.ChangeHeaders,
            ModificationActions.ChangeHeader("X-Repeated", "rewritten", index: 1));

        await Converse(
            "EHLO test.example.com",
            "MAIL FROM:<random@example.com>",
            "RCPT TO:<root@localhost.localdomain>",
            "DATA",
            "X-Repeated: first\r\nX-Repeated: second\r\nSubject: Subject\r\n\r\nBody\r\n.",
            "QUIT");

        var delivered = await ReceiveMail();

        Assert.That(delivered, Does.Contain("X-Repeated: first"));
        Assert.That(delivered, Does.Contain("X-Repeated: rewritten"));
        Assert.That(delivered, Does.Not.Contain("X-Repeated: second"));
    }

    [Test]
    public async Task Deleting_a_header_reaches_the_delivered_mail()
    {
        RespondAtEndOfBody(ActionRequirements.DeleteHeaders, ModificationActions.DeleteHeader("Subject"));

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Doomed subject", "Body"));

        Assert.That(await ReceiveMail(), Does.Not.Contain("Doomed subject"));
    }

    [Test]
    public async Task Replacing_the_body_reaches_the_delivered_mail()
    {
        RespondAtEndOfBody(
            ActionRequirements.ChangeBody,
            ModificationActions.ReplaceBody(new MemoryStream(Encoding.ASCII.GetBytes("Body written by the filter\r\n"))));

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body written by the sender"));

        var delivered = await ReceiveMail();

        Assert.That(delivered, Does.Contain("Body written by the filter"));
        Assert.That(delivered, Does.Not.Contain("Body written by the sender"));
    }

    [Test, CancelAfter(30_000)]
    public async Task Adding_a_recipient_delivers_the_mail_twice()
    {
        RespondAtEndOfBody(
            ActionRequirements.AddRecipients,
            ModificationActions.AddRecipient("nobody@localhost.localdomain"));

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));

        var firstDelivery = await ReceiveMail();
        var secondDelivery = await ReceiveMail();

        // The pipe transport prepends Delivered-To, so the envelope recipient is visible in the message.
        Assert.That(
            firstDelivery + secondDelivery,
            Does.Contain("Delivered-To: root@localhost.localdomain")
                .And.Contains("Delivered-To: nobody@localhost.localdomain"));
    }

    [Test, CancelAfter(60_000)]
    public async Task Deleting_the_only_recipient_stops_the_delivery()
    {
        RespondAtEndOfBody(
            ActionRequirements.DeleteRecipients,
            ModificationActions.DeleteRecipient("root@localhost.localdomain"));

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));

        await Assert.ThatAsync(
            async () => await ReceiveMail().WaitAsync(TimeSpan.FromSeconds(20)),
            Throws.InstanceOf<TimeoutException>());
    }

    // A quarantined message is held in the queue instead of being delivered.
    //
    // The reason deliberately goes unasserted, because against Postfix it is unobservable. Postfix reads
    // the reason off SMFIR_QUARANTINE and then logs its own fixed "milter triggers HOLD action" text, and
    // it holds the message just as readily when the reason is missing altogether - which was measured
    // here, not assumed. So the reason reaching the wire is only provable at the byte level; see
    // ResponseFrameFactoryTests.Quarantining_carries_the_reason.
    [Test, CancelAfter(30_000)]
    public async Task Quarantining_holds_the_message_in_the_queue()
    {
        RespondAtEndOfBody(
            ActionRequirements.QuarantineMessage,
            ModificationActions.QuarantineMessage("held by the integration test"));

        await SendMail(new("random@example.com", "root@localhost.localdomain", "Subject", "Body"));

        var heldMessageCount = await SubmitMailLocally("find /var/spool/postfix/hold -type f | wc -l");

        Assert.That(heldMessageCount.Trim(), Is.EqualTo("1"), "The message should have been held.");
        Assert.That(await ReadPostfixLog(), Does.Contain("milter-hold"));
    }

    // Answers end-of-body with the given actions and then accepts the message.
    private void RespondAtEndOfBody(ActionRequirements actionRequirements, params Action[] actions)
    {
        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.EndOfBody;
        MilterContextAdapter.ActionRequirements = actionRequirements;

        ContextAdapter
            .OnEndOfBody()
            .ReturnsForAnyArgs(actions.Append<Action>(CommitActions.Accept).ToAsyncEnumerable());
    }

    // Asserts that the callback matching the given protocol requirements was actually invoked.
    //
    // Postfix answers 4.7.1 both when the filter defers and when it cannot reach the filter at all, since
    // milter_default_action defaults to tempfail. Reading the SMTP reply alone therefore proves nothing: a
    // milter that crashed on the first packet, or was never connected to, produces the same 4.7.1.
    private void AssertDecidingCallbackRan(ProtocolRequirements protocolRequirements)
    {
        switch (protocolRequirements)
        {
            case ProtocolRequirements.Connect:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnConnectionEstablished(default, default);
                break;

            case ProtocolRequirements.ClientHello:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnClientHello(default, default);
                break;

            case ProtocolRequirements.Sender:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnSender(default, default);
                break;

            case ProtocolRequirements.Recipient:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnRecipient(default, default);
                break;

            case ProtocolRequirements.Header:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnHeader(default, default);
                break;

            case ProtocolRequirements.EndOfHeaders:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnEndOfHeaders(default);
                break;

            case ProtocolRequirements.Body:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnBodyChunk(default, default);
                break;

            case ProtocolRequirements.EndOfBody:
                _ = ContextAdapter.ReceivedWithAnyArgs().OnEndOfBody();
                break;

            default:
                Assert.Fail($"No callback is mapped to {protocolRequirements}.");
                break;
        }
    }

    private static string LoadMultiChunkBody()
    {
        var resourceName = $"{typeof(MilterTests).Namespace}.MultiChunkBody.txt";

        using var stream = typeof(MilterTests).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded resource {resourceName} is missing.");

        using var reader = new StreamReader(stream, Encoding.ASCII);

        // Normalized rather than taken as read. The raw string literal this replaced drew its line endings
        // from the working tree, so the expected body quietly depended on the checkout's autocrlf setting.
        return reader.ReadToEnd().ReplaceLineEndings("\r\n").TrimEnd('\r', '\n');
    }
}