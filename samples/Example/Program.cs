// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Example;
using Josupeit.Mail.Milter;

const int PORT = 8894;

// Hosts MilterContext on a TCP socket. Point Postfix at it with
// smtpd_milters = inet:127.0.0.1:8894 in main.cf and reload.
using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

socket.Bind(new IPEndPoint(IPAddress.Loopback, PORT));
socket.Listen();

// Pass a factory - new Milter<MilterContext>(socket, () => new MilterContext(logger)) - when the
// filter needs something handed in. Without one the context comes from Activator.CreateInstance,
// which requires a public parameterless constructor.
var milter = new Milter<MilterContext>(socket);

milter.Start();

Console.WriteLine($"Milter listening on {IPAddress.Loopback}:{PORT}. Press Ctrl+C to stop.");

await WaitForShutdown();

// Stop waits for the sessions still in flight, so a message being filtered right now is not cut off.
await milter.Stop();

static Task WaitForShutdown()
{
    var shutdown = new TaskCompletionSource();

    Console.CancelKeyPress += (_, eventArgs) =>
    {
        // Without this the runtime terminates the process here and Stop never runs.
        eventArgs.Cancel = true;

        shutdown.TrySetResult();
    };

    return shutdown.Task;
}
