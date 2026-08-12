# Josupeit.Mail.Milter

A .NET implementation of the **Milter protocol** - the mail filter protocol spoken by Postfix and Sendmail - so a mail filter can be written in C# instead of in the C the protocol was designed around.

An MTA connects to a milter over a socket and streams the SMTP conversation to it as it happens. After each event the filter answers, and the answer decides what the MTA does next: continue, accept, reject, defer or discard. At the end of the message the filter may also rewrite it - headers, recipients, body - or quarantine it.

## Packages

| Package                                                      | Description                                                                                                                                              |
| ------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`Josupeit.Mail.Milter`](src/Josupeit.Mail.Milter/README.md) | The library. Implement `IMilterContext`, hand it to `Milter<TContext>`, and the wire protocol, framing and protocol-state enforcement are taken care of. |

Targets `net8.0` and speaks **wire version 2** of the protocol, which every current Postfix and Sendmail accepts.

A runnable filter lives in [`samples/Example`](samples/Example) - roughly thirty lines, including the socket setup and a graceful shutdown.

## Building and testing

```bash
# Build the entire solution
dotnet build Josupeit.Mail.Milter.slnx

# Run the unit tests - no Docker needed
dotnet test test/Josupeit.Mail.Milter.UnitTests

# Run everything, including the integration tests
dotnet test Josupeit.Mail.Milter.slnx
```

The solution is in the `.slnx` format, which needs a recent SDK.

There are two test projects, and the split is deliberate. `Josupeit.Mail.Milter.UnitTests` needs nothing but the SDK and covers what a correct MTA can never produce: malformed frames, protocol violations, byte-exact serialization, the abort and replay states. `Josupeit.Mail.Milter.IntegrationTests` **requires Docker** - it builds an Ubuntu image with a real Postfix, points its `smtpd_milters` back at a milter hosted by the test, and asserts on the mail that actually comes out the other end.

## Contributing

[CONTRIBUTING.md](CONTRIBUTING.md) holds the code style and the commit-message rules; [CLAUDE.md](CLAUDE.md) describes what the project is and how the runtime fits together.

Commits are stamped with a licence header by a hook that is not installed by default:

```bash
git config core.hooksPath .githooks
```

## License

[EUPL-1.2](https://joinup.ec.europa.eu/collection/eupl/eupl-text-eupl-12)
