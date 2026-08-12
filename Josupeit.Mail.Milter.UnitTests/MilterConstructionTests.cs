using System;
using System.Net.Sockets;
using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class MilterConstructionTests
{
    private Socket _socket = null!;

    [SetUp]
    public void SetUp()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    [TearDown]
    public void TearDown()
    {
        _socket.Dispose();
    }

    [Test]
    public void A_context_factory_can_be_supplied()
    {
        var milter = new Milter<TestMilterContext>(_socket, () => new TestMilterContext());

        Assert.That(milter, Is.Not.Null);
    }

    [Test]
    public void A_missing_context_factory_is_rejected()
    {
        Assert.That(
            () => new Milter<TestMilterContext>(_socket, contextFactory: null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void A_missing_socket_is_rejected()
    {
        Assert.That(() => new Milter<TestMilterContext>(null!), Throws.ArgumentNullException);
        Assert.That(
            () => new Milter<TestMilterContext>(null!, () => new TestMilterContext()),
            Throws.ArgumentNullException);
    }
}
