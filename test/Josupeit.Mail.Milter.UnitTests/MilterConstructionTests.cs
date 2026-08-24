// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

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
        Assert.Multiple(() =>
        {
            Assert.That(() => new Milter<TestMilterContext>(null!), Throws.ArgumentNullException);
            Assert.That(
                () => new Milter<TestMilterContext>(null!, () => new TestMilterContext()),
                Throws.ArgumentNullException);
        });
    }

    // Options used to be optional on both constructors, which made new Milter<T>(socket, null) resolve
    // to neither of them. There are four overloads and no optional parameters now, so passing options
    // means having some - and null is a mistake rather than a way of asking for the defaults, which is
    // what the shorter overload is for.
    [Test]
    public void Missing_options_are_rejected_where_options_are_asked_for()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new Milter<TestMilterContext>(_socket, (MilterOptions)null!), Throws.ArgumentNullException);
            Assert.That(
                () => new Milter<TestMilterContext>(_socket, () => new TestMilterContext(), null!),
                Throws.ArgumentNullException);
        });
    }

    [Test]
    public void Options_can_be_supplied_with_or_without_a_context_factory()
    {
        var withoutFactory = new Milter<TestMilterContext>(_socket, new MilterOptions());
        var withFactory = new Milter<TestMilterContext>(_socket, () => new TestMilterContext(), new MilterOptions());

        Assert.Multiple(() =>
        {
            Assert.That(withoutFactory, Is.Not.Null);
            Assert.That(withFactory, Is.Not.Null);
        });
    }
}
