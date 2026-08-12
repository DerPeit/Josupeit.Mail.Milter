# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Read [CONTRIBUTING.md](CONTRIBUTING.md) before writing code or committing.** It holds the code style (layout, naming, statement grouping, async, performance mindset, tests) and the commit-message rules. This file covers what the project is and how it works.

## What this is

A .NET library that implements the **Milter protocol** (the Sendmail/Postfix mail filter protocol, wire version 2). An MTA (Postfix/Sendmail) connects to a milter over TCP and streams SMTP events; the milter inspects them and replies with accept/reject/modify decisions. This library lets you write that filter in C#: implement `IMilterContext`, hand it to `Milter<TContext>`, and the library handles the wire protocol, framing, and protocol-state enforcement.

## Build & test

```pwsh
dotnet build Josupeit.Mail.Milter.slnx          # solution is .slnx (XML format), needs a recent SDK
dotnet test  Josupeit.Mail.Milter.slnx
dotnet test  test/Josupeit.Mail.Milter.UnitTests            # the Docker-free half, seconds instead of minutes
dotnet test --filter "Name~Milter_receives_correct_body"   # single test by name
```

**The integration tests require Docker.** `Josupeit.Mail.Milter.IntegrationTests` uses Testcontainers to build an Ubuntu+Postfix image (`Dockerfile.postfix`) and run a real Postfix that connects back to a milter hosted by the test on the host machine. `Josupeit.Mail.Milter.UnitTests` is Docker-free and covers what a correct MTA can never produce: malformed frames, protocol violations, and the abort/replay states.

- `PostfixFixture` builds/starts the container and exposes `SendMail` / `ReceiveMail`.
- `MilterFixture` binds the milter on host port **34000** and the mail-receiving socket on **35000**; `main.cf` points Postfix's `smtpd_milters` at `host.docker.internal:34000` and pipes delivered mail (`mailbox_transport = tcp-loopback` -> `nc` in `master.cf`) back to `35000` so tests can read the resulting message.
- Tests use NSubstitute `ForPartsOf` partial mocks of `MilterContextAdapter`. `ProtocolRequirements`/`ActionRequirements` are **static** on the context, so tests set them as statics in `Setup`.

## Projects

The tree is split three ways, and the split decides what a project inherits. `src/Directory.Build.props` carries everything to do with shipping a package, so anything under `src/` is packable unless it opts out; `test/Directory.Build.props` carries the test-runner packages and the reference to the library, so the test projects are nearly empty files; `samples/` inherits neither, which is why the example lives there rather than under `src/`.

| Project                                      | TFM            | Role                                                     |
| -------------------------------------------- | -------------- | -------------------------------------------------------- |
| `src/Josupeit.Mail.Milter`                   | net8.0         | The library, and the only package. Namespace root `Josupeit.Mail.Milter`. |
| `src/Josupeit.Mail.Milter.Generator`         | netstandard2.0 | Roslyn source generator. Experimental (see bottom), not packable. |
| `test/Josupeit.Mail.Milter.IntegrationTests` | net8.0         | NUnit + Testcontainers integration tests. Needs Docker.  |
| `test/Josupeit.Mail.Milter.UnitTests`        | net8.0         | NUnit unit tests over the internal pipeline. No Docker.  |
| `samples/Example`                            | net8.0         | Skeleton showing a user `IMilterContext` implementation. |

## Packaging

`src/Josupeit.Mail.Milter` is the only package. Everything about shipping it lives in `src/Directory.Build.props`, so a project is packable by virtue of sitting under `src/` - which is why the generator opts out with `IsPackable=false` and the example lives under `samples/` instead.

- **Versions come from the git history**, via Nerdbank.GitVersioning: `version.json` at the root sets the base version, `src/Josupeit.Mail.Milter/version.json` sets the release tag pattern (`v{version}`). Nothing hand-edits a version number, and a shallow clone gets it wrong - hence `fetch-depth: 0` in CI.
- **Release notes come from the git log.** `GeneratePackageReleaseNotes` in `src/Directory.Build.targets` runs `git log --pretty=format:"- %s"` over the commits touching the project since the previous release tag. Commit subjects are read by consumers, which is the practical reason the Conventional Commits rule in CONTRIBUTING.md matters.
- **`GenerateDocumentationFile` is on**, so CS1591/CS1573 police the doc comments. See CONTRIBUTING.md for the rule they enforce.
- SourceLink needs no `PackageReference`: since .NET 8 the SDK supplies the GitHub provider for a repository whose origin is github.com. **Moving to Codeberg means adding `Microsoft.SourceLink.Gitea` and a `SourceLinkGiteaHost` item**, because the SDK ships no Gitea provider.
- The package carries an SBOM, a `snupkg` with embedded sources, `LICENSE.md` and the project's own `README.md`.

**The generator is packed into the library, not depended on.** Its `ProjectReference` is `PrivateAssets="all"` so NuGet does not emit a dependency on a package that will never exist, and `IncludeGeneratorAssemblyInPackage` puts its assembly into `lib/` by hand - the library's metadata names the marker attribute types, so the assembly carrying them has to travel with it. Both halves disappear if the generator is ever dropped.

Verify a packaging change by unzipping the result, not by trusting a green `dotnet pack`: `dotnet pack src/Josupeit.Mail.Milter -c Release -o packages` should produce `lib/net8.0/` holding the library, its `.xml` doc file and the generator assembly, plus `README.md`, `LICENSE.md`, an `_manifest/` and dependencies on nothing but the four runtime packages.

## How a filter is written (the user-facing model)

1. Implement `IMilterContext` (which extends `IMilterCallbacks`) as a **`partial class`**.
2. Override only the `On*` callbacks you care about (`OnConnectionEstablished`, `OnClientHello`, `OnSender`, `OnRecipient`, `OnHeader`, `OnEndOfHeaders`, `OnBodyChunk`, `OnEndOfBody`, `OnDefineMacro`). All have default implementations returning `CommitActions.Continue`.
3. Declare the two static requirement properties yourself:
   - `static ProtocolRequirements ProtocolRequirements` - which SMTP events you want delivered. Should match the callbacks you actually overrode (Milter negotiation lets the filter tell the MTA which events to skip, so declaring fewer events means the MTA sends fewer).
   - `static ActionRequirements ActionRequirements` - which message modifications you intend to perform.
4. `new Milter<TContext>(socket).Start();` - or `new Milter<TContext>(socket, () => new TContext(logger))` when the context needs dependencies. Without a factory the context comes from `Activator.CreateInstance`, which requires a public parameterless constructor.

See `MilterContextAdapter` in the test project for a complete implementation that declares both properties.

Callbacks return `ValueTask<CommitAction>` except `OnEndOfBody`, which returns `IAsyncEnumerable<Action>`: zero or more `ModificationAction`s and `ProgressAction`s, then **exactly one** terminal `CommitAction`. That sequence is enforced - yielding nothing, omitting the commit action, or yielding anything after it throws `InvalidOperationException`. Build actions via the `CommitActions` and `ModificationActions` static factories. `OnBodyChunk` takes `ReadOnlySpan<byte>`.

## Runtime architecture

`Milter<TContext>` accepts socket connections and runs one **`Session`** per connection on a long-running task. The data path inside a session is built on `System.IO.Pipelines` and bounded `Channel`s (capacity 1) split into three layers:

```
NetworkStream
  └─ PipeReader/Writer
       ├─ RequestParser  (bg task): LinkReader  -> RequestFrame  -> request channel
       └─ ResponseFormatter (bg task): response channel -> ResponseFrame -> LinkWriter
Session.Run loop:
  RequestFrame -> RequestPacketFactory -> RequestPacket
              -> SessionController (RequestPacketVisitor) -> current IProtocolState
              -> ResponsePacket -> ResponseFrameFactory -> response channel
```

Three layers, each in its own folder:

- **`Link/`** - wire format. `LinkReader`/`LinkWriter` (big-endian length prefix + 1 command byte + data), `RequestFrame`/`ResponseFrame`, `RequestParser`/`ResponseFormatter`, `CommandCode` (maps to `SMFIC_*`), and the negotiation flag enums `ActionDiscriminator` (`SMFIF_*`) and `ContentBypassDiscriminator` (`SMFIP_*`).
- **`Protocol/`** - strongly-typed packets. `RequestPacket`/`ResponsePacket` hierarchies, their visitors (`RequestPacketVisitor`, `ResponsePacketVisitor`), and the factories that convert frames <-> packets.
- **`Session/`** - the protocol state machine plus `ContextAdapter`.

### Protocol state machine (`Session/ProtocolEnforcement/`)

State pattern enforcing legal event ordering. The base `ProtocolState` throws "protocol violation" for *every* event; each concrete state overrides only the events it accepts. `SessionController` dispatches each request packet to the current state, which yields response packets and transitions the machine.

Flow: `AwaitingNegotiationState` -> (after OPTNEG, validating version/action/content-bypass support) -> either `NegotiatedState` or `ReplayRecordingNegotiatedState`.

The **Replay** states handle Postfix keeping a connection alive across multiple messages: connection/macro info is sent once, but per-message states need it again. When `Connect` is required, `ReplayRecordingNegotiatedState` records the connect/macro packets so `ReplayingConnectedState` can replay them for subsequent messages. `ConnectedState`/`ReplayingConnectedState` handle per-message events, `*AbortedState` handle ABORT, `DisconnectedState` is the final (terminal) state ending the `Session.Run` loop.

### `ContextAdapter`

Bridges protocol packets and the user's context. Lazily creates **one context instance per message**, not per session: the states dispose the adapter at every message boundary (ABORT, QUIT, and the end of a message), so the next message on the same connection gets a fresh context from the supplied factory or `Activator.CreateInstance`. A context may therefore hold per-message state, and must not be relied on to see two messages. The adapter converts request packets into the friendly `*Information` structs, invokes the callback, and turns the returned `CommitAction`/`Action` into `ResponsePacket`s. It disposes the context if it implements `IDisposable`/`IAsyncDisposable`.

### Actions

`Action` (abstract record) has three branches: `CommitAction` (Continue/Accept/Reject/Discard/Defer/Reply) decides the message's fate, `ModificationAction` (AddHeader/ChangeHeader/DeleteHeader/AddRecipient/DeleteRecipient/ReplaceBody/**Quarantine**) changes it, and `ProgressAction` sits directly under `Action` because it does neither - it only tells the MTA to restart its reply timeout. Each knows how to produce its `ResponsePacket`(s) via `CreateResponsePackets`.

`CommitActions`/`ModificationActions` are the public factories and the **only** way to build an action: the records have internal constructors and get-only properties, so neither `new` nor a `with` expression can bypass the validation the factories perform (control characters are rejected in every outbound string; header names must be RFC 5322 ftext). The simple commit actions are cached singletons, as are `ProgressAction.Instance` - progress belongs to neither factory, so it is reached through that property.

## Conventions & gotchas

- Internals are exposed to both test projects through `<InternalsVisibleTo />` entries in `src/Josupeit.Mail.Milter/Josupeit.Mail.Milter.csproj` (the SDK emits the attribute) - the tests reach straight into the internal pipeline. `DynamicProxyGenAssembly2` is granted the same from `test/Directory.Build.props`, which is what lets NSubstitute mock internal types.
- NSubstitute cannot mock `ReadOnlySpan<byte>`, so the test `MilterContextAdapter` implements `IMilterCallbacks.OnBodyChunk` explicitly and forwards to a `byte[]` overload that *can* be mocked. Keep that shim if you touch body handling in tests.
- `ProtocolRequirements.EndOfBody = 0` ("cannot be excluded") - it is always part of the protocol; the flag value being 0 is intentional.
- Dependencies of note: `Nito.AsyncEx` (`AsyncMonitor` for session-count coordination in `Milter`), `System.IO.Pipelines`, `System.Linq.Async` (`IAsyncEnumerable` LINQ).

## Source generator (under development - `Josupeit.Mail.Milter.Generator`)

**Status: experimental, under active development and consideration.** It is not yet a relied-upon part of the authoring model - for now, context implementations declare `ProtocolRequirements` by hand (see above). Do not assume generated code exists when reasoning about how a filter behaves.

The goal is to remove the manual `ProtocolRequirements` step by computing that bitmask from which callbacks the user actually overrode:

- Each callback on `IMilterCallbacks` is tagged `[MilterCallbackMarker(nameof(ProtocolRequirements.X))]`.
- For each user context class, the generator uses `FindImplementationForInterfaceMember` per callback. If the implementation **equals the interface default** (i.e. the user did not override it), the flag is dropped; otherwise `ProtocolRequirements.X` is OR'd in.
- It emits a `partial class` with the `static ProtocolRequirements` getter. If the user already declared that property, the generator skips the class (so a hand-written declaration always wins).

**It currently leaks Roslyn into consumer output.** `src/Josupeit.Mail.Milter/Josupeit.Mail.Milter.csproj` references the generator as a normal `ProjectReference` rather than an `Analyzer` one, because the library needs the marker attribute types that live in it. So `Josupeit.Mail.Milter.Generator.dll` ships in the output directory and `Microsoft.CodeAnalysis.CSharp`/`.Common` land in `deps.json`. Known, documented in the csproj, and deliberately not fixed while the generator's future is open - deleting it makes the problem go away.

Notes when touching the generator:
- It is an `ISourceGenerator` (v1), not incremental. A commented-out `ISyntaxContextReceiver`/incremental version exists at the bottom of `MilterContextSourceGenerator.cs`.
- Marker attributes (`MilterContextMarkerAttribute`, `MilterCallbackMarkerAttribute`, `ProtocolRequirementsMarkerAttribute`) are matched **by assembly+type name string** (`IsDescribedBy`), not by symbol reference, because the generator carries its own copies of the attribute types. The `*Locator`/`*Visitor` classes are the symbol-tree search helpers.
