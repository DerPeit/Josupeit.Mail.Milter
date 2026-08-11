using System.Net.Sockets;
using System.Net;
using NSubstitute;

namespace Josupeit.Mail.Milter.IntegrationTests;

public abstract class MilterFixture : PostfixFixture
{
    private static readonly TimeSpan SessionDrainTimeout = TimeSpan.FromSeconds(10);

    private readonly Socket _milterSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

    private Milter<MilterContextAdapter> _milter;
    private MilterContextAdapter _contextAdapter;

    private protected Func<MilterContextAdapter> ContextAdapterResolver { get; set; }

    private protected MilterContextAdapter ContextAdapter => ContextAdapterResolver();

    public override async Task OneTimeSetup()
    {
        await base.OneTimeSetup();

        _milterSocket.Bind(new IPEndPoint(IPAddress.Any, 34000));
        _milterSocket.Listen();

        _milter = new Milter<MilterContextAdapter>(_milterSocket, contextFactory: () => ContextAdapterResolver());
        _milter.Start();
    }

    public override async Task Setup()
    {
        await base.Setup();

        MilterContextAdapter.ProtocolRequirements = ProtocolRequirements.EndOfBody;
        MilterContextAdapter.ActionRequirements = ActionRequirements.None;

        ContextAdapterResolver = Substitute.For<Func<MilterContextAdapter>>();
        ContextAdapterResolver().Returns(_ => _contextAdapter ??= Substitute.ForPartsOf<MilterContextAdapter>());
    }

    public override async Task TearDown()
    {
        // The container goes first, because its connections are what keep the milter sessions alive. Then
        // the sessions have to be waited for: clearing the resolver underneath one still in flight either
        // fails it with a NullReferenceException or, worse, hands it the next test's context adapter and
        // records callbacks against the wrong test.
        await base.TearDown();

        var sessionsEnding = _milter.WaitForSessionsToEnd();

        Assert.That(
            await Task.WhenAny(sessionsEnding, Task.Delay(SessionDrainTimeout)),
            Is.SameAs(sessionsEnding),
            "Milter sessions were still running after the Postfix container stopped.");

        await sessionsEnding;

        _contextAdapter = null;
        ContextAdapterResolver = null;
    }

    public override async Task OneTimeTearDown()
    {
        await _milter.Stop();

        _milterSocket.Dispose();

        _milter = null;

        await base.OneTimeTearDown();
    }
}