using System.Net.Sockets;
using System.Net;
using NSubstitute;

namespace Josupeit.Mail.Milter.IntegrationTests;

public abstract class MilterFixture : PostfixFixture
{
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

    public override Task TearDown()
    {
        _contextAdapter = null;
        ContextAdapterResolver = null;

        return base.TearDown();
    }

    public override async Task OneTimeTearDown()
    {
        await _milter.Stop();

        _milterSocket.Dispose();

        _milter = null;

        await base.OneTimeTearDown();
    }
}