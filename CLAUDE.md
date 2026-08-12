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

| Project                                      | TFM            | Role                                                                      |
| -------------------------------------------- | -------------- | ------------------------------------------------------------------------- |
| `src/Josupeit.Mail.Milter`                   | net8.0         | The library, and the only package. Namespace root `Josupeit.Mail.Milter`. |
| `src/Josupeit.Mail.Milter.Generator`         | netstandard2.0 | Roslyn source generator (see bottom). Not packable on its own.            |
| `test/Josupeit.Mail.Milter.IntegrationTests` | net8.0         | NUnit + Testcontainers integration tests. Needs Docker.                   |
| `test/Josupeit.Mail.Milter.UnitTests`        | net8.0         | NUnit unit tests over the internal pipeline. No Docker.                   |
| `samples/Example`                            | net8.0         | Skeleton showing a user `IMilterContext` implementation.                  |

## Packaging

`src/Josupeit.Mail.Milter` is the only package. Everything about shipping it lives in `src/Directory.Build.props`, so a project is packable by virtue of sitting under `src/` - which is why the generator opts out with `IsPackable=false` and the example lives under `samples/` instead.

- **Versions come from the git history**, via Nerdbank.GitVersioning: `version.json` at the root sets the base version, `src/Josupeit.Mail.Milter/version.json` sets the release tag pattern (`v{version}`). Nothing hand-edits a version number, and a shallow clone gets it wrong - hence `fetch-depth: 0` in CI.
- **Release notes come from the git log.** `GeneratePackageReleaseNotes` in `src/Directory.Build.targets` runs `git log --pretty=format:"- %s"` over the commits touching the project since the previous release tag. Commit subjects are read by consumers, which is the practical reason the Conventional Commits rule in CONTRIBUTING.md matters.
- **`GenerateDocumentationFile` is on**, so CS1591/CS1573 police the doc comments. See CONTRIBUTING.md for the rule they enforce.
- SourceLink needs no `PackageReference`: since .NET 8 the SDK supplies the GitHub provider for a repository whose origin is github.com. **Moving to Codeberg means adding `Microsoft.SourceLink.Gitea` and a `SourceLinkGiteaHost` item**, because the SDK ships no Gitea provider.
- The package carries an SBOM, a `snupkg` with embedded sources, `LICENSE.md` and the project's own `README.md`.

**The generator is packed into the library as an analyzer, not depended on.** Its `ProjectReference` is `OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all"`, so nothing of it reaches this assembly's metadata or `deps.json`, and NuGet emits no dependency on a package that will never exist. `IncludeGeneratorAsAnalyzerInPackage` puts the assembly under `analyzers/dotnet/cs/` by hand. See the generator section at the bottom for how the library gets the marker attributes without referencing it.

Verify a packaging change by unzipping the result, not by trusting a green `dotnet pack`: `dotnet pack src/Josupeit.Mail.Milter -c Release -o packages` should produce `lib/net8.0/` holding the library and its `.xml` doc file **and nothing else**, `analyzers/dotnet/cs/` holding the generator assembly, plus `README.md`, `LICENSE.md`, an `_manifest/` and dependencies on nothing but the four runtime packages.

## How a filter is written (the user-facing model)

1. Implement `IMilterContext` (which extends `IMilterCallbacks`) as a **`partial class`**.
2. Override only the `On*` callbacks you care about (`OnConnectionEstablished`, `OnClientHello`, `OnSender`, `OnRecipient`, `OnHeader`, `OnEndOfHeaders`, `OnBodyChunk`, `OnEndOfBody`, `OnDefineMacro`). All have default implementations returning `CommitActions.Continue`.
3. Leave the two static requirement properties to the source generator, or declare either by hand to override it:
   - `static ProtocolRequirements ProtocolRequirements` - which SMTP events you want delivered. Derived from the callbacks you actually overrode (Milter negotiation lets the filter tell the MTA which events to skip, so declaring fewer events means the MTA sends fewer).
   - `static ActionRequirements ActionRequirements` - which message modifications you intend to perform. Derived from the actions your code builds.
4. `new Milter<TContext>(socket).Start();` - or `new Milter<TContext>(socket, () => new TContext(logger))` when the context needs dependencies. Without a factory the context comes from `Activator.CreateInstance`, which requires a public parameterless constructor.

`samples/Example` declares neither property and relies on the generator. `MilterContextAdapter` in the integration tests declares both, as mutable statics, because the tests rewrite the negotiation per case.

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

State pattern enforcing legal event ordering. The base `ProtocolState` throws "protocol violation" for _every_ event; each concrete state overrides only the events it accepts. `SessionController` dispatches each request packet to the current state, which yields response packets and transitions the machine.

Flow: `AwaitingNegotiationState` -> (after OPTNEG, validating version/action/content-bypass support) -> either `NegotiatedState` or `ReplayRecordingNegotiatedState`.

The **Replay** states handle Postfix keeping a connection alive across multiple messages: connection/macro info is sent once, but per-message states need it again. When `Connect` is required, `ReplayRecordingNegotiatedState` records the connect/macro packets so `ReplayingConnectedState` can replay them for subsequent messages. `ConnectedState`/`ReplayingConnectedState` handle per-message events, `*AbortedState` handle ABORT, `DisconnectedState` is the final (terminal) state ending the `Session.Run` loop.

### `ContextAdapter`

Bridges protocol packets and the user's context. Lazily creates **one context instance per message**, not per session: the states dispose the adapter at every message boundary (ABORT, QUIT, and the end of a message), so the next message on the same connection gets a fresh context from the supplied factory or `Activator.CreateInstance`. A context may therefore hold per-message state, and must not be relied on to see two messages. The adapter converts request packets into the friendly `*Information` structs, invokes the callback, and turns the returned `CommitAction`/`Action` into `ResponsePacket`s. It disposes the context if it implements `IDisposable`/`IAsyncDisposable`.

### Actions

`Action` (abstract record) has three branches: `CommitAction` (Continue/Accept/Reject/Discard/Defer/Reply) decides the message's fate, `ModificationAction` (AddHeader/ChangeHeader/DeleteHeader/AddRecipient/DeleteRecipient/ReplaceBody/**Quarantine**) changes it, and `ProgressAction` sits directly under `Action` because it does neither - it only tells the MTA to restart its reply timeout. Each knows how to produce its `ResponsePacket`(s) via `CreateResponsePackets`.

`CommitActions`/`ModificationActions` are the public factories and the **only** way to build an action: the records have internal constructors and get-only properties, so neither `new` nor a `with` expression can bypass the validation the factories perform (control characters are rejected in every outbound string; header names must be RFC 5322 ftext). The simple commit actions are cached singletons, as are `ProgressAction.Instance` - progress belongs to neither factory, so it is reached through that property.

## Conventions & gotchas

- Internals are exposed to both test projects through `<InternalsVisibleTo />` entries in `src/Josupeit.Mail.Milter/Josupeit.Mail.Milter.csproj` (the SDK emits the attribute) - the tests reach straight into the internal pipeline. `DynamicProxyGenAssembly2` is granted the same from `test/Directory.Build.props`, which is what lets NSubstitute mock internal types.
- NSubstitute cannot mock `ReadOnlySpan<byte>`, so the test `MilterContextAdapter` implements `IMilterCallbacks.OnBodyChunk` explicitly and forwards to a `byte[]` overload that _can_ be mocked. Keep that shim if you touch body handling in tests.
- `ProtocolRequirements.EndOfBody = 0` ("cannot be excluded") - it is always part of the protocol; the flag value being 0 is intentional.
- Dependencies of note: `Nito.AsyncEx` (`AsyncMonitor` for session-count coordination in `Milter`), `System.IO.Pipelines`, `System.Linq.Async` (`IAsyncEnumerable` LINQ).

## Source generator (`Josupeit.Mail.Milter.Generator`)

It writes the two `IMilterContext` requirement properties, and **nothing else**. Both are `static abstract` interface members, which is the safety net the whole design leans on: refusing to emit one fails the build with CS0535 rather than negotiating something wrong. A property the author declared by hand is always left alone.

### `ProtocolRequirements` - from the callbacks

- Each callback on `IMilterCallbacks` is tagged `[MilterCallbackMarker(nameof(ProtocolRequirements.X))]`. `OnDefineMacro` and `OnEndOfBody` carry none: neither has a flag to clear.
- `FindImplementationForInterfaceMember` per callback. An implementation that **equals the interface default** means nobody overrode it, so the flag is dropped; otherwise `ProtocolRequirements.X` is OR'd in. An explicit interface implementation counts as an override.

### `ActionRequirements` - from the actions the code builds

Decidable only as an over-approximation, and that is exactly what the negotiation wants: over-declaring is harmless on the wire, under-declaring makes the MTA refuse the modification with **no runtime check anywhere in this library** to catch it. So `ActionRequirementsAnalysis` is a may-analysis - it unions every action the code mentions and never asks whether a branch is reachable. **Control flow therefore costs nothing**: an action behind an `if`, in a loop, in a `catch`, in a lambda counts like one on the straight path.

- Each concrete `ModificationAction` record is tagged `[MilterActionMarker(nameof(ActionRequirements.X))]`; `Action` and `ModificationAction` carry a bare `[MilterActionMarker]` marking them as the abstract types a modification can hide behind. `CommitAction` and `ProgressAction` deliberately carry none. The generator never names an action type itself - `ActionTypes` reads it all off the attributes.
- The walk seeds on the context type, its partials and its base types, then follows every invoked member that has a body **in this compilation**, with a visited set for cycles. Any expression whose type resolves to a marked concrete action contributes its flag.
- When an action arrives from something with no body here - an interface, an abstract member, another assembly - it reports **MILT002** and emits nothing, so CS0535 forces a hand-written declaration.
- One deliberate soundness hole, `PassesActionsThrough`: a member whose parameters, receiver or type arguments mention an action is assumed to be moving actions rather than building them, so `ToAsyncEnumerable`, `Select` and `Concat` do not trip MILT002. A generic _factory_ that builds an action from a type argument alone reads the same way and is not caught. Without this the analysis fires on ordinary LINQ and is unusable.
- The other known gap: a public field of action type written from outside the analysed code.

### Notes when touching it

- It is an `IIncrementalGenerator`. The whole analysis runs in the pipeline's `transform`, so what reaches `RegisterSourceOutput` is `MilterContextEmission` - strings and `Diagnostic`s, never symbols.
- Marker attributes are matched **by full type name** (`IsDescribedBy`), not by symbol reference and deliberately **not** by assembly: two assemblies compile the same declaration, so the attribute found on a symbol is never the one the generator can call `typeof` on.
- Diagnostic ids live in `GeneratorDiagnostics` and are tracked in `AnalyzerReleases.Unshipped.md`, which RS2008 enforces. That file's table format is what Roslyn's parser wants, not what prettier would write - it is in `.prettierignore`.
- `Environment.NewLine` is banned by `EnforceExtendedAnalyzerRules`; the generator uses its own `NEW_LINE` const.
- The project targets netstandard2.0, so the types modern C# compiles down to are missing. **PolySharp** supplies them, from the root `Directory.Build.props`, along with the `EmbeddedAttribute` the markers wear. See the comment there for the one assumption that rests on.
- Tests are `MilterContextSourceGeneratorTests` in the unit project, driving the generator through `CSharpGeneratorDriver` (`GeneratorHarness`). They assert on the generated text **and** on compiling it, which is what catches emitted code that merely looks right.

### How the marker attributes reach the library

The library applies the markers but must not reference the generator's assembly, or the compiler comes along with it into every consumer's `deps.json`. So **each marker attribute is two partials**:

- `Markers/XMarkerAttribute.cs` - the declaration, `internal sealed partial`, naming nothing outside `System`. Compiled into **both** projects: the generator project holds the file, and `Josupeit.Mail.Milter.csproj` links it in under `Properties\`.
- `Markers/XMarkerAttribute.Symbols.cs` - `IsDescribedBy` and the constructor-argument readers, which need `Microsoft.CodeAnalysis`. Compiled into the generator only.

Both halves live in namespace `Josupeit.Mail.Milter.Generator.Markers`, mirroring the folder.

Two consequences worth knowing before touching either half:

- **Nothing Roslyn-shaped may enter the declaration half**, or it lands in the library. The one thing those files do take from the `Microsoft.CodeAnalysis` namespace is `[Embedded]`, and that resolves to **PolySharp's** generated `EmbeddedAttribute` rather than to anything in the package - so `using Microsoft.CodeAnalysis;` there is not the dependency it looks like.
- **The markers are `[Embedded]`**, so a referencing compilation does not import them at all - a consumer cannot name them (verified: CS0234), and a project referencing both the library and the generator does not get two ambiguous copies. The generator still reads them off a symbol's `GetAttributes()`, which is unaffected.

The library's `ProjectReference` to the generator is therefore `OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all"`, and `IncludeGeneratorAsAnalyzerInPackage` packs `@(Analyzer)` into `analyzers/dotnet/cs/`. That is the only copy in the package, and the only one the compiler loads - drop it and the generation silently stops running for consumers while everything still builds here.

Analyzers do not flow across a `ProjectReference` the way they do across a `PackageReference`, so `samples/Example` asks for the generator itself. Verify a change to any of this the way it was verified when it was made: pack, then build a throwaway project against the `.nupkg` and look at the generated file and at `deps.json`.
