using System.Net;
using DotNet.Testcontainers.Configurations;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;

namespace Josupeit.Mail.Milter.Tests;

public abstract class PostfixFixture
{
    private readonly IFutureDockerImage _image;
    private readonly IVolume _spoolVolume;
    private readonly IVolume _libVolume;
    private readonly Socket _mailSocket;
    private readonly IContainer _container;

    private SmtpClient _smtpClient;
    private DateTime _startTimestamp;

    protected PostfixFixture()
    {
        _image = new ImageFromDockerfileBuilder().WithDockerfile("Dockerfile.postfix").Build();
        _spoolVolume = new VolumeBuilder().Build();
        _libVolume = new VolumeBuilder().Build();

        _mailSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

        _container = new ContainerBuilder()
            .WithImage(_image)
            .WithPortBinding(25, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(25, o => o.WithTimeout(TimeSpan.FromSeconds(30))))
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithResourceMapping("master.cf", "/etc/postfix/", UnixFileModes.UserRead)
            .WithResourceMapping("main.cf", "/etc/postfix/", UnixFileModes.UserRead)
            .WithVolumeMount(_spoolVolume, "/var/spool/postfix")
            .WithVolumeMount(_libVolume, "/var/lib/postfix")
            .Build();
    }

    [OneTimeSetUp]
    public virtual async Task OneTimeSetup()
    {
        await _image.CreateAsync(TestContext.CurrentContext.CancellationToken);
        await _spoolVolume.CreateAsync(TestContext.CurrentContext.CancellationToken);
        await _libVolume.CreateAsync(TestContext.CurrentContext.CancellationToken);

        _mailSocket.Bind(new IPEndPoint(IPAddress.Any, 35000));
        _mailSocket.Listen();
    }

    [SetUp]
    public virtual async Task Setup()
    {
        _startTimestamp = DateTime.Now;

        await _container.StartAsync(TestContext.CurrentContext.CancellationToken);

        _smtpClient = new SmtpClient(_container.Hostname, _container.GetMappedPublicPort(25));
    }

    [TearDown]
    public virtual async Task TearDown()
    {
        _smtpClient.Dispose();

        var (stdout, _) = await _container.GetLogsAsync(_startTimestamp, timestampsEnabled: false);
        await _container.StopAsync();

        await TestContext.Out.WriteAsync(stdout);
    }

    [OneTimeTearDown]
    public virtual async Task OneTimeTearDown()
    {
        await _container.DisposeAsync();
        _mailSocket.Dispose();
        await _spoolVolume.DisposeAsync();
        await _libVolume.DisposeAsync();
        await _image.DisposeAsync();
    }

    protected Task SendMail(MailMessage message)
    {
        return _smtpClient.SendMailAsync(message, TestContext.CurrentContext.CancellationToken);
    }

    protected async Task<string> ReceiveMail()
    {
        using var socket = await _mailSocket.AcceptAsync(TestContext.CurrentContext.CancellationToken);
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        return await reader.ReadToEndAsync(TestContext.CurrentContext.CancellationToken);
    }
}
