using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using NSubstitute;

namespace Josupeit.Mail.Milter.IntegrationTests;

[TestOf(typeof(Milter<>)), CancelAfter(10_000)]
public class MilterTests : MilterFixture
{
    /// <summary>
    /// A body long enough to cross Postfix's MILTER_CHUNK_SIZE, so it arrives as several SMFIC_BODY frames
    /// and the body test covers reassembly rather than a single chunk.
    /// </summary>
    private static readonly string MultiChunkBody = LoadMultiChunkBody();

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
            Body = MultiChunkBody,
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