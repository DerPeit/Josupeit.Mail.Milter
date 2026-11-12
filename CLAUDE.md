# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Read [CONTRIBUTING.md](CONTRIBUTING.md) before writing code or committing.** It holds the code style (layout, naming, statement grouping, async, performance mindset, tests) and the commit-message rules. This file covers what the project is and how it works.

## What this is

A .NET library that implements the **Milter protocol** (the Sendmail/Postfix mail filter protocol, wire version 6, negotiated down to what the MTA offers with a floor of 2). An MTA (Postfix/Sendmail) connects to a milter over TCP and streams SMTP events; the milter inspects them and replies with accept/reject/modify decisions. This library lets you write that filter in C#: implement `IMilterContext`, hand it to `Milter<TContext>`, and the library handles the wire protocol, framing, and protocol-state enforcement.

## Build & test

```pwsh
dotnet build Josupeit.Mail.Milter.slnx          # solution is .slnx (XML format), needs a recent SDK
dotnet test  Josupeit.Mail.Milter.slnx
dotnet test  test/Josupeit.Mail.Milter.UnitTests            # the Docker-free half, seconds instead of minutes
dotnet test --filter "Name~Milter_receives_correct_body"   # single test by name
dotnet tool restore                                        # once per clone; nbgv is a local tool
```

**The integration tests require Docker.** `Josupeit.Mail.Milter.IntegrationTests` uses Testcontainers to build an Ubuntu+Postfix image (`Dockerfile.postfix`) and run a real Postfix that connects back to a milter hosted by the test on the host machine. `Josupeit.Mail.Milter.UnitTests` is Docker-free and covers what a correct MTA can never produce: malformed frames, protocol violations, and the abort/replay states.

**CI runs the unit tests only.** Codeberg's shared runners are container jobs with no Docker daemon of their own, so the integration half cannot run there - it is a local step before pushing, not a gate. `.forgejo/workflows/ci.yml` is the whole pipeline; there is no `.github/` any more.

- `PostfixFixture` builds/starts the container and exposes `SendMail` / `ReceiveMail`.
- `MilterFixture` binds the milter on host port **34000** and the mail-receiving socket on **35000**; `main.cf` points Postfix's `smtpd_milters` at `host.docker.internal:34000` and pipes delivered mail (`mailbox_transport = tcp-loopback` -> `nc` in `master.cf`) back to `35000` so tests can read the resulting message.
- Tests use NSubstitute `ForPartsOf` partial mocks of `MilterContextAdapter`. `ProtocolRequirements`/`ActionRequirements` are **static** on the context, so tests set them as statics in `Setup`.

## Projects

The tree is split three ways, and the split decides what a project inherits. `src/Directory.Build.props` carries everything to do with shipping a package, so anything under `src/` is packable unless it opts out; `test/Directory.Build.props` carries the test-runner packages and the reference to the library, so the test projects are nearly empty files; `samples/Directory.Build.props` carries nothing but `IsPackable=false`, which is the whole reason the example lives there rather than under `src/`. That one line is load-bearing: without it a sample is packable by default, and a solution-wide `dotnet pack` drops an `Example.1.0.0.nupkg` into the directory the release step pushes from.

All three import the repository-wide `Directory.Build.props` by hand, because MSBuild reads only the nearest one.

| Project                                      | TFM            | Role                                                                      |
| -------------------------------------------- | -------------- | ------------------------------------------------------------------------- |
| `src/Josupeit.Mail.Milter`                   | net8.0         | The library, and the only package. Namespace root `Josupeit.Mail.Milter`. |
| `src/Josupeit.Mail.Milter.Generator`         | netstandard2.0 | Roslyn source generator (see bottom). Not packable on its own.            |
| `test/Josupeit.Mail.Milter.IntegrationTests` | net8.0         | NUnit + Testcontainers integration tests. Needs Docker.                   |
| `test/Josupeit.Mail.Milter.UnitTests`        | net8.0         | NUnit unit tests over the internal pipeline. No Docker.                   |
| `samples/Example`                            | net8.0         | Skeleton showing a user `IMilterContext` implementation.                  |

## Packaging

`src/Josupeit.Mail.Milter` is the only package. Everything about shipping it lives in `src/Directory.Build.props`, so a project is packable by virtue of sitting under `src/` - which is why the generator opts out with `IsPackable=false` and the example lives under `samples/` instead.

- **Versions come from the git history**, via Nerdbank.GitVersioning: `version.json` at the root sets the base version, `src/Josupeit.Mail.Milter/version.json` sets the release tag pattern (`v{version}`). Nothing hand-edits a version number, and a shallow clone gets it wrong - which is why the CI checkout is a plain full `git clone` rather than anything truncated.
- **Release notes come from the git log.** `GeneratePackageReleaseNotes` in `src/Directory.Build.targets` runs `git log --pretty=format:"- %s"` over the commits touching the project since the previous release tag. Commit subjects are read by consumers, which is the practical reason the Conventional Commits rule in CONTRIBUTING.md matters.
- **`GenerateDocumentationFile` is on**, so CS1591/CS1573 police the doc comments. See CONTRIBUTING.md for the rule they enforce.
- **SourceLink is wired by hand**, in `src/Directory.Build.props`: the SDK contributes a provider for each host it knows and Gitea is not one of them, so the origin being codeberg.org buys nothing. `Microsoft.SourceLink.Gitea` plus a `SourceLinkGiteaHost` item name the host. Removing either fails nothing - the package simply loses source stepping.
- The package carries an SBOM, a `snupkg` with embedded sources, `LICENSE.md` and the project's own `README.md`.

**The generator is packed into the library as an analyzer, not depended on.** Its `ProjectReference` is `OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all"`, so nothing of it reaches this assembly's metadata or `deps.json`, and NuGet emits no dependency on a package that will never exist. `IncludeGeneratorAsAnalyzerInPackage` puts the assembly under `analyzers/dotnet/cs/` by hand. See the generator section at the bottom for how the library gets the marker attributes without referencing it.

Verify a packaging change by unzipping the result, not by trusting a green `dotnet pack`: `dotnet pack src/Josupeit.Mail.Milter -c Release -o packages` should produce `lib/net8.0/` holding the library and its `.xml` doc file **and nothing else**, `analyzers/dotnet/cs/` holding the generator assembly, plus `README.md`, `LICENSE.md`, `llms.txt`, `llms-full.txt`, an `_manifest/` and a dependency on `System.IO.Pipelines` and nothing else.

## LLM documentation

`src/Josupeit.Mail.Milter` carries `llms.txt` and `llms-full.txt` ([llmstxt.org](https://llmstxt.org/) format, hyphenated as the convention has it), packed into the NuGet next to the README by `src/Directory.Build.props`. The structure follows the reference implementation in the sibling Josupeit.Practices repository.

- **`llms.txt`** is the index: H1 package name, blockquote summary, a heading-free body with namespace, install command and type list, then `## Documentation` (llms-full.txt, PublicAPI.Shipped.txt, README) and `## Optional`.
- **`llms-full.txt`** is the self-contained reference, in this order: At a glance, Concepts, Quick start, the type catalog (summary table, then one `###` per type with its exact C# declaration), the source generator and its diagnostics, Contract expectations, Usage, a choosing guide, the wire layer, Common mistakes, Related links, License.
- Declarations are written as C# under `#nullable enable`, never in the `PublicAPI.Shipped.txt` format. That file is linked as the machine-verified inventory, never inlined, and `PublicAPI.Unshipped.txt` is never linked.
- **Keep concrete API facts out of this file.** A probe agent starts with CLAUDE.md in its context, so a fact stated here contaminates a probe that asks about it. The facts belong in `llms-full.txt`.
- Links to repository files are absolute raw Codeberg URLs on `main` (`https://codeberg.org/DerPeit/Josupeit.Mail.Milter/raw/branch/main/...`); source-tree links use the web UI URL. `main` rather than `development` on purpose: the published copy describes the latest release, the packaged copy its own version. A file added on `development` therefore 404s from the links until the next release merge, and that is correct.
- The blockquote summary is identical in both files.
- Both files are formatted by prettier as markdown (the override in `.prettierrc`) with CRLF line endings (`.editorconfig` and `.gitattributes` name them individually - `*.txt` would also catch the PublicAPI files and the byte-exact test fixture). Run `npx prettier@3 --write "**/llms.txt" "**/llms-full.txt"` after editing.
- The repository root carries `llms.txt` (the index for the repository, linking the package's `llms-full.txt`) and `context7.json` (Context7 parsing configuration and the library-wide rules; each rule is at most 255 characters, the schema's limit). `context7.json` pins `"branch": "main"`, because `development` is the default branch here and Context7 would otherwise index unreleased documentation. `.forgejo/workflows/context7.yml` triggers a refresh on every push to `main` using the `CONTEXT7_API_KEY` repository secret. Claiming the library on context7.com adds a `url`/`public_key` pair to `context7.json`, which proves ownership.
- `scripts/check-llms-docs.sh` is the drift guard and runs as the first CI step: every packable project has its pair, the H1 equals the PackageId, the blockquote is identical in both files, every repository link resolves on `main`, every type and member in the PublicAPI files is mentioned in `llms-full.txt` as a whole word, and the root `llms.txt` links the pair. The compiler-generated record members (`<Clone>$`, `EqualityContract`, `PrintMembers`, `Deconstruct`, `Equals`, `GetHashCode`, `ToString`, the equality operators, `init` accessors) are skipped. Run it locally before committing; a public API change the documentation does not mention fails the build.
- Before committing a changed pair, **compile every snippet against the packed package**, not a `ProjectReference` - the generator only runs the way a consumer sees it from the `.nupkg`. One scratch project outside the repository with a `nuget.config` pointing at a local feed: every positive snippet in one build that must succeed without warnings, a runtime check exercising the library's own types for every behavioural claim, and one isolated build per documented failure that must fail with the predicted diagnostic id.
- Then **probe it with a context-free agent**: copy `llms-full.txt` to a neutral path, give a fresh agent only that file and the library's trap questions (namespace, callback signatures, the `Action` clash, the end-of-body sequence, context lifetime, generator diagnostics, libmilter-style assumptions), and require a line citation from the copy for every answer. An uncited answer counts as `DOC SILENT`, and every `DOC SILENT` is a gap to close. A README-only control run shows which answers the document actually changed.

## Branching & releasing

`development` is the default branch and where feature branches land; `main` is the release branch.

**Feature branches land on `development` through a Codeberg pull request**, not a local merge. The PR is the gate: CI runs the build, the unit tests and the AOT smoke test against the branch, and the merge only happens after the pipeline passes. Create the PR with `gh pr create --base development`, and merge it from the Codeberg UI or with `gh pr merge`. The merge strategy is merge commit (no squash, no rebase), so a feature keeps its own commits.

**Every merge commit carries a written message, never the default.** `Merge branch 'x' into y` on its own repeats what the graph already shows. Say what the branch was for and why it was worth doing, exactly as for any other commit - and more so for a merge into `main`, whose message is the nearest thing to a release description the history holds. Pass it with `git merge -F <file>`: `git merge` does not read a message from stdin, so `-F -` fails.

`main` is not merely a convention here. `version.json` sets `publicReleaseRefSpec` to `^refs/heads/main$`, so only a build off `main` produces a clean version - anywhere else Nerdbank.GitVersioning appends the commit id (`0.1.10-g3ec122c51c`). The Pack and Push steps in `.forgejo/workflows/ci.yml` are gated on the same ref, which is why nothing publishes from `development`. **A push to `main` therefore publishes**, as far as the pipeline is concerned - the only thing standing between a merge and nuget.org is the `NUGET_API_KEY` secret existing on the repository.

The merge from `development` into `main` stays local - it is the release step, not a feature landing, and there is no pipeline to exercise that the `development` PR has not already run:

After merging `development` into `main`:

1. Dry-run the tag to see the version it would carry:

   ```pwsh
   dotnet nbgv tag --what-if -p src/Josupeit.Mail.Milter
   ```

   It prints the tag without creating it - `v0.1.10`, from the `v{version}` pattern in `src/Josupeit.Mail.Milter/version.json` plus the commit height.

2. Reword the merge commit to the release convention:

   ```pwsh
   git commit --amend -m "chore(release): 0.1"
   ```

   Major.minor, and no package name: there is only one package here, unlike the multi-package repository this convention is borrowed from.

3. Create the tag for real:

   ```pwsh
   dotnet nbgv tag -p src/Josupeit.Mail.Milter
   ```

The tag does more than name the version. `GeneratePackageReleaseNotes` finds the previous release with `git describe --match "v*"`, so the next version's notes cover exactly the commits since this tag. Skip the tag and the following release repeats the whole history instead.

### nbgv notes

- It is a **local dotnet tool**: `dotnet nbgv`, never bare `nbgv`, and `dotnet tool restore` has to have been run once in the clone.
- `-p` takes the **project directory** (`src/Josupeit.Mail.Milter`), not the `.csproj`.
- `--what-if` is the dry run for the `tag` subcommand.

## How a filter is written (the user-facing model)

1. Implement `IMilterContext` (which extends `IMilterCallbacks`) as a **`partial class`**.
2. Override only the `On*` callbacks you care about (`OnConnectionEstablished`, `OnClientHello`, `OnSender`, `OnRecipient`, `OnMessageDataStarted`, `OnUnknownCommand`, `OnHeader`, `OnEndOfHeaders`, `OnBodyChunk`, `OnEndOfBody`, `OnDefineMacro`). All have default implementations returning `CommitActions.Continue`.
3. Leave the six static requirement properties to the source generator, or declare any of them by hand to override it:
   - `static abstract ProtocolRequirements ProtocolRequirements` - which SMTP events you want delivered. Derived from the callbacks you actually overrode (Milter negotiation lets the filter tell the MTA which events to skip, so declaring fewer events means the MTA sends fewer).
   - `static abstract ActionRequirements ActionRequirements` - which message modifications you intend to perform. Derived from the actions your code builds.
   - `static virtual NoReplyRequirements NoReplyRequirements` - which events the MTA need not wait for an answer to. Derived by **proving** a callback can only ever answer `Continue`.
   - `static virtual DeliveryRequirements DeliveryRequirements` - `RejectedRecipients` is derived from whether the code reads `RecipientInformation.IsRejectedByMta`; `LeadingHeaderSpace` is never derived and needs a hand-written declaration.
   - `static virtual MacroRequirements MacroRequirements` - which macros you want per stage. Derived from the stage-guarded constant lookups in `OnDefineMacro`, or **MILT003** when they cannot be followed.
   - `static virtual ProtocolVersionRequirement ProtocolVersionRequirement` - the lowest protocol version the MTA must speak. A fold over what the events, the actions and the delivery flags turned out to need. The macro list and the no-reply family contribute nothing on purpose: an older MTA simply sends its own macro set and waits for every answer, so demanding version 6 for either would refuse an MTA the filter works perfectly well against.
4. `new Milter<TContext>(socket).Start();` - or `new Milter<TContext>(socket, () => new TContext(logger))` when the context needs dependencies. Without a factory the context comes from `Activator.CreateInstance`, which requires a public parameterless constructor.

**The split between `static abstract` and `static virtual` is deliberate.** The first two have no safe default - "want nothing" disables the filter and "modify nothing" breaks its modifications - so CS0535 is the net under both. The other four default to "behave exactly as version 2 did", which makes the generator an optimiser for them rather than a load-bearing part: a consumer who loses the analyzer gets a filter that still works.

`samples/Example` declares none of them and relies on the generator. `MilterContextAdapter` in the integration tests declares the first two, as mutable statics, because the tests rewrite the negotiation per case - and its callbacks are `virtual`, which is why no-reply is never derived for it (see the analysis note below).

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

Flow: `AwaitingNegotiationState` -> (after OPTNEG, clamping the version and masking every flag to what it defines) -> either `NegotiatedState` or `ReplayRecordingNegotiatedState`, chosen by `NegotiatedStateFactory`. `SMFIC_QUIT_NC` returns to that same pair from anywhere `SMFIC_QUIT` is accepted, with a **fresh** replay history: a different client follows, so the recorded connect packets stop being true - unlike after an abort, which keeps them.

The **Replay** states handle Postfix keeping a connection alive across multiple messages: connection/macro info is sent once, but per-message states need it again. When `Connect` is required, `ReplayRecordingNegotiatedState` records the connect/macro packets so `ReplayingConnectedState` can replay them for subsequent messages. `ConnectedState`/`ReplayingConnectedState` handle per-message events, `*AbortedState` handle ABORT, `DisconnectedState` is the final (terminal) state ending the `Session.Run` loop.

### `ContextAdapter`

Bridges protocol packets and the user's context. Lazily creates **one context instance per message**, not per session: the states dispose the adapter on ABORT, QUIT and QUIT_NC, so the next message on the same connection normally gets a fresh context from the supplied factory or `Activator.CreateInstance`. A context may therefore hold per-message state, and must not be relied on to see two messages. Nothing disposes it after end-of-body, though, so an MTA that starts a second message without an intervening ABORT or QUIT_NC reuses the adapter - which is why the skip flags are cleared when end-of-body finishes rather than on disposal. The adapter converts request packets into the friendly `*Information` structs, invokes the callback, and turns the returned `CommitAction`/`Action` into `ResponsePacket`s. It disposes the context if it implements `IDisposable`/`IAsyncDisposable`.

### Actions

`Action` (abstract record) has three branches: `CommitAction` (Continue/Accept/Reject/Discard/Defer/Reply) decides the message's fate - plus **SkipRemainingEvents**, which decides nothing at all and shares the branch only because it is an answer to an event, `ModificationAction` (AddHeader/**InsertHeader**/ChangeHeader/DeleteHeader/AddRecipient_V2/**AddRecipient_V6**/DeleteRecipient/ReplaceBody/**ChangeSender**/**Quarantine**) changes it, and `ProgressAction` sits directly under `Action` because it does neither - it only tells the MTA to restart its reply timeout. Each knows how to produce its `ResponsePacket`(s) via `CreateResponsePackets`.

`CommitActions`/`ModificationActions` are the public factories and the **only** way to build an action: the records have internal constructors and get-only properties, so neither `new` nor a `with` expression can bypass the validation the factories perform (control characters are rejected in every outbound string except that a header value may carry tabs and fold its lines with LF or CRLF plus whitespace, normalised to bare LF on the wire because Postfix splits on LF alone; header names must be RFC 5322 ftext). The simple commit actions are cached singletons, as are `ProgressAction.Instance` - progress belongs to neither factory, so it is reached through that property.

## Conventions & gotchas

- Internals are exposed to both test projects through `<InternalsVisibleTo />` entries in `src/Josupeit.Mail.Milter/Josupeit.Mail.Milter.csproj` (the SDK emits the attribute) - the tests reach straight into the internal pipeline. `DynamicProxyGenAssembly2` is granted the same from `test/Directory.Build.props`, which is what lets NSubstitute mock internal types.
- NSubstitute cannot mock `ReadOnlySpan<byte>`, so the test `MilterContextAdapter` implements `IMilterCallbacks.OnBodyChunk` explicitly and forwards to a `byte[]` overload that _can_ be mocked. Keep that shim if you touch body handling in tests.
- **`ProtocolRequirements` and `ActionRequirements` members append, never insert.** The generator emits flags in enum declaration order and the tests assert on whole expression strings, so a member added in the middle flips them. `ProtocolStage` is exempt - nothing serialises its ordinals, and `ResponseFrameFactory` maps it to the `SMFIM_` codes explicitly on the way out.
- **The `All` composites stay narrow.** `ContentBypassDiscriminator.All` means "every skippable event" and `ActionDiscriminator.All` means "every modification"; the no-reply family, skip, the delivery flags and `SetMacroList` are all deliberately outside them, because both composites are used as a stand-in for a permissive MTA. `NegotiationFlagTests` pins the membership.
- **`main.cf` pins `milter_protocol = 6`.** The downshift to 2 is covered by the unit tests rather than by the container; run the integration suite against both by hand when touching negotiation.
- `ProtocolRequirements.EndOfBody = 0` ("cannot be excluded") - it is always part of the protocol; the flag value being 0 is intentional.
- Dependencies of note: `System.IO.Pipelines`. That is the only runtime dependency. `Nito.AsyncEx` and `System.Interactive.Async` were replaced with internal implementations (`Coordination/AsyncMonitor` and `Linq/AsyncEnumerableEx`) to remove unannotated dependencies that blocked Native AOT compatibility. The test projects and the sample take a direct Ix reference for their own use.

## Source generator (`Josupeit.Mail.Milter.Generator`)

It writes the six `IMilterContext` requirement properties, and **nothing else**. A property the author declared by hand is always left alone.

The first two are `static abstract`, which is the safety net they lean on: refusing to emit one fails the build with CS0535 rather than negotiating something wrong. The other four are `static virtual` with a safe default, so the generator optimises rather than enables them - and MILT002 and MILT003 must therefore stay errors, because for the virtual ones the diagnostic is the only gate.

### `ProtocolRequirements` - from the callbacks

- Each callback on `IMilterCallbacks` is tagged `[MilterCallbackMarker(nameof(ProtocolRequirements.X), nameof(NoReplyRequirements.X), MinimumProtocolVersion = N)]`. `OnDefineMacro` and `OnEndOfBody` carry none: neither has a flag to clear.
- `FindImplementationForInterfaceMember` per callback. An implementation that **equals the interface default** means nobody overrode it, so the flag is dropped; otherwise `ProtocolRequirements.X` is OR'd in. An explicit interface implementation counts as an override.

### `ActionRequirements` - from the actions the code builds

Decidable only as an over-approximation, and that is exactly what the negotiation wants: over-declaring is harmless on the wire, under-declaring makes the MTA refuse the modification with **no runtime check anywhere in this library** to catch it. So `ActionRequirementsAnalysis` is a may-analysis - it unions every action the code mentions and never asks whether a branch is reachable. **Control flow therefore costs nothing**: an action behind an `if`, in a loop, in a `catch`, in a lambda counts like one on the straight path.

- Each concrete `ModificationAction` record is tagged `[MilterActionMarker(nameof(ActionRequirements.X), MinimumProtocolVersion = N)]`; `Action` and `ModificationAction` carry a bare `[MilterActionMarker]` marking them as the abstract types a modification can hide behind. `CommitAction` and `ProgressAction` deliberately carry none - except `SkipRemainingEventsAction`, which carries a nameless marker purely to declare its protocol version, because `SMFIP_SKIP` is an event bit and needs no modification right. The generator never names an action type itself - `ActionTypes` reads it all off the attributes.
- The walk seeds on the context type, its partials and its base types, then follows every invoked member that has a body **in this compilation**, with a visited set for cycles. Any expression whose type resolves to a marked concrete action contributes its flag.
- When an action arrives from something with no body here - an interface, an abstract member, another assembly - it reports **MILT002** and emits nothing, so CS0535 forces a hand-written declaration.
- One deliberate soundness hole, `PassesActionsThrough`: a member whose parameters, receiver or type arguments mention an action is assumed to be moving actions rather than building them, so `ToAsyncEnumerable`, `Select` and `Concat` do not trip MILT002. A generic _factory_ that builds an action from a type argument alone reads the same way and is not caught. Without this the analysis fires on ordinary LINQ and is unusable.
- The other known gap: a public field of action type written from outside the analysed code.

### `NoReplyRequirements` - a must-analysis, and the inverse of the one above

`SMFIP_NR_*` tells the MTA not to wait for an answer. `NoReplyRequirementsAnalysis` sets a flag only after **proving** a callback can answer nothing but `Continue`, which makes it a must-analysis where `ActionRequirements` is a may-analysis - and that inversion is the whole safety argument. A callback it cannot prove keeps its reply, which is what the library did before any of this existed, so every way of losing track lands on the safe side and there is no diagnostic.

- `ContinueAction` carries `[MilterUnobservableActionMarker]`, which says _why_ rather than _which_: answering it and not answering are the same thing to the MTA. The generator never learns the type's name.
- The commit action base is read off the callback's own return type, so the hierarchy is still never named.
- Two conditions, and the second is easy to forget: every commit action the body mentions must be unobservable, **and** it must mention at least one. A body mentioning none has not been proved, merely read.
- **Only the body is walked.** A declaration also carries `ValueTask<CommitAction>`, whose `CommitAction` is not unobservable - walking the whole declaration proves every callback capable of deciding and derives nothing at all, silently.
- **An overridable callback on an unsealed context proves nothing**, because the body that runs may belong to a derived type. Not hypothetical: the integration suite's own adapter is a partial mock whose `virtual` callbacks return `Continue` and whose substitute returns `Reject`.

### `DeliveryRequirements` and `MacroRequirements`

- `DeliveryRequirements.RejectedRecipients` is a may-analysis over reads of the member carrying `[MilterDeliveryMarker]` - reading `IsRejectedByMta` is the whole evidence. `LeadingHeaderSpace` can never be derived, so a filter wanting it declares the property by hand and names both members itself.
- `MacroRequirements` reads stage-guarded constant lookups of the member carrying `[MilterMacroSourceMarker]`, correlating each with the stage guard around it (`if`, `switch`, a switch expression, or a stage comparison on the left of an `&&` - its right operand only runs once the left one held, so `stage == X && Macros.TryGetValue(...)` is guarded). Anything it cannot pin down reports **MILT003**. Strict like MILT002, and for the same reason: a list that leaves a macro out fails at runtime rather than at negotiation.
- The `SMFIM_` stage codes are **not** `ProtocolStage`'s own numbers, and two stages have none at all, so the mappable ones carry `[MilterMacroStageMarker]` and the generator refuses to emit a call it would only compile to throw.

### `ProtocolVersionRequirement` - a fold, not a walk

The maximum of the `MinimumProtocolVersion` on the markers the event, action and delivery derivations already visited. No separate analysis. Resolved by **constant value** rather than by member name - the fold computes a number and must find the member carrying it, because a cast would print a version the enum does not declare - and it starts at the floor rather than at zero for the same reason.

**It only covers what the generator derived.** A property declared by hand contributes nothing to the fold, which is why the negotiation does not rely on it alone. `AwaitingNegotiationState` fails the session in two further cases, both of them silent otherwise: a modification the MTA will not grant - checked against what the filter asked for, before the version mask, since masking first is what made a version 6 action against an older MTA disappear without trace - and `LeadingHeaderSpace` being withheld, which is the one event flag whose loss changes bytes rather than merely costing a round trip. Naming macros is exempt from the first check: `SMFIF_SETSYMLIST` changes nothing about the message, so an MTA that declines it just sends its own set.

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
