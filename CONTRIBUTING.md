# Contributing

Conventions for this repository: **how code is written** and **how commits are worded**.
For what the project _is_ and how it is built, see [CLAUDE.md](CLAUDE.md).

Some of this is machine-enforced, most of it is not.

`.editorconfig` carries the naming rules, file layout and encoding, and `Directory.Build.props` sets `EnforceCodeStyleInBuild`, so a violation is a build warning rather than a mark in the editor - without that property the rules colour the IDE and nothing else. `ConfigureAwaitChecker.Analyzer` covers the `ConfigureAwait` rule. `src/Directory.Build.props` sets `GenerateDocumentationFile`, which is what turns the doc-comment rules below into build warnings: CS1591 for an undocumented public member, CS1573 for a parameter without its own tag. Neither is suppressed, and both are expected to stay at zero.

Everything else below is a convention no analyzer can express: statement grouping, the visibility tiers, the `ValueTask` policy, the `Async`-suffix ban, the test layout. One naming rule is only _partly_ enforced - see the note on the exposed-constants rule in `.editorconfig`. Where a rule appears in both files, keep them saying the same thing.

## Code style

### Files & layout

- **One type per file**, file name == type name.
- **Folders mirror namespaces** (`Link/`, `Protocol/`, `Session/`, `Session/ProtocolEnforcement/`).
- **File-scoped namespaces** (`namespace X;`).
- `using` directives **outside** the namespace, `System.*` first, then the rest, each block alphabetical. **No** global or implicit usings.
- **4 spaces**, no tabs. **Allman** braces.
- Line endings: **CRLF** in the working tree (Git normalizes to LF in the repository).
- **Every `.cs` file opens with the licence header**, before the `using` block and separated from it by a blank line:

  ```csharp
  // SPDX-License-Identifier: EUPL-1.2
  // Copyright (c) Manuel Josupeit-Walter
  ```

  A contributor who touches a file adds their own `// Copyright (c) <name>` line under the existing ones. This is not busywork to do by hand - `.githooks/pre-commit` stamps it into every staged file, taking the name from `git config user.name`. The hook is not installed by default, so enable it once per clone:

  ```bash
  git config core.hooksPath .githooks
  ```

### Naming & visibility

- **Three visibility tiers**, not one default. This is a library, and the wire layer is deliberately reachable so a consumer can extend it rather than fork it:
  - **The authoring surface** - `Milter<T>`, `IMilterContext`, `IMilterCallbacks`, `Action` and its subtypes, the `CommitActions`/`ModificationActions` factories, the `*Information` types, `MilterOptions`, the requirement enums. Public, and the only tier a normal filter touches.
  - **The extensible wire layer** - `LinkReader`/`LinkWriter`, `RequestParser`/`ResponseFormatter`, `RequestPacketFactory`/`ResponseFrameFactory`, the packet hierarchies and their visitors, plus the `I*` interfaces over all of them. **Public and non-sealed on purpose**, with `protected internal virtual` members, so a consumer can subclass a visitor or swap a factory. Adding a packet type means adding a `Visit*` to the matching visitor - that is a breaking change for anyone who derived, so weigh it.
  - **Everything else** - `internal sealed`: the session, the protocol states, `ContextAdapter`, `WireText`. Reachable from the two test projects through `<InternalsVisibleTo />` and from nowhere else.
- **Sealed unless it is meant to be derived from.** A public type that is not sealed is a statement that overriding it is supported; do not unseal one just to make a test easier.
- **Validate arguments on the public surface**, not on internal members. An internal method is only reached by this library and its tests, so a guard there checks a caller that cannot get it wrong.
- **`const` is `SCREAMING_SNAKE_CASE`** - field or local, `CHUNK_SIZE`, `END_OF_NAME_MARK_LENGTH`. The case marks the value as baked in at compile time.
- **The screaming snake stops at the API boundary.** It is not idiomatic C#, so nothing visible from outside wears it: the public surface is `PascalCase` throughout, constants included. `MilterOptions.DefaultMaxFrameLength`, never `DEFAULT_MAX_FRAME_LENGTH`. Today every constant in the library happens to be private or local, which is why none of them is affected.
- **Private fields are `_camelCase`**, including `static readonly` ones (`_stopTimeout`).
- Locals and parameters `camelCase`; everything else `PascalCase`.
- Interfaces are `I`-prefixed.
- **No `Async` suffix** on async methods - `Session.Run`, `Milter.Stop` and `LinkWriter.WriteData` are all async. Exception: names dictated by a framework contract (`DisposeAsync`).
- Names spell out the domain term rather than the wire mnemonic: `OnConnectionEstablished`, not `OnConnect`; `ContentBypassDiscriminator`, not `SmfipFlags`. Map the `SMFI*` name in a comment or enum member instead.

### Comments

Two kinds, and which one to use is decided by who can see the member - not by how much there is to say.

- **`///` doc comments are for the public surface of `Josupeit.Mail.Milter`**, and nothing else: public members of public types, protected members of public types. They are what a consumer reads in IntelliSense and what ends up in the generated XML file.
- **`//` comments everywhere else** - non-public members of the library, and every other project in the solution. Same prose, no tags: `<see cref="X"/>` becomes the bare name, `<see langword="null"/>` becomes `null`, a `<summary>` and a `<remarks>` become one block split by a bare `//` line.
- **A doc comment is complete or it is wrong.** `<summary>` always, plus a tag for every parameter, type parameter, return value and exception the member documents. Half a doc comment is worse than none: IntelliSense shows the summary and silently omits the parameter the caller was actually asking about.
- **`<inheritdoc />` wherever a base or interface already says it.** An override or an interface implementation repeats nothing; the one description then cannot drift from its copies.
- Explain **why**, not what. The signature already says what a member is called and what it takes. The doc comment earns its place by saying what the caller cannot see: what is guaranteed, what is not, what the value means when it is `null`, and which mistake the API is shaped to prevent.

### Language features

`LangVersion` is `latest`, `Nullable` is `enable`. Use modern C# freely:

- **`var`** for locals; **target-typed `new()`** for returns and field initializers.
- **Primary constructors** for injected dependencies, e.g. `internal sealed class ContextAdapter(Func<IMilterCallbacks> contextFactory)` - **only where readability does not suffer**:
  - The parameter list must **fit on the declaration line**. If it would wrap, write an explicit constructor instead.
  - As soon as a **second constructor** is needed, **all** constructors are explicit. Never mix a primary constructor with an overload.
- **Records** for data: `abstract record` / `sealed record` for the `Action` hierarchy, **`readonly record struct`** for the small `*Information` payloads handed to callbacks.
- **Switch expressions** with a trailing comma on the last arm; blank line between the arms of a switch _statement_.
- Nullable annotations and `??=`, `?.` where they shorten the code.
- **Omit braces** for single-statement `if` / `foreach` / `await foreach` bodies. Braces once the body spans more than one line.
- Package references use **bounded version ranges** (`[8,9)`), not floating versions.

### Statement grouping

**Insert a blank line at every transition between statement groups; no blank line within a group.**

| Group                 | Examples                                     |
| --------------------- | -------------------------------------------- |
| Variable declarations | `var x = ...;` `Type x = ...;`               |
| Assignments           | `x = ...;` `x.Prop = ...;`                   |
| Invocations           | `x.Method(...);` (local-variable receiver)   |
| Static invocations    | `Type.Method(...);`                          |
| Keywords              | `return` / `await` / `throw` / `yield`       |
| Multi-line statements | always alone - blank line before _and_ after |

Classify by the **outermost** construct:

- `await x.Foo()` -> keyword (not an invocation)
- `var x = await x.Foo()` -> variable declaration
- `var x = new Foo { ... }` spanning lines -> multi-line statement

An extra blank line between two groups of the _same_ kind is fine when it separates logical steps.

### Async

- **`ConfigureAwait(false)` on every await** - enforced by `ConfigureAwaitChecker.Analyzer`; do not omit it.
- **`ValueTask<T>` yes; non-generic `ValueTask` usually not.** Measured on net8.0: when the result is already
  known, an `async Task<T>` or a `Task.FromResult(value)` costs **72 bytes** per call and the `ValueTask<T>`
  equivalents cost **nothing**; on suspension the two are equal. So `ValueTask<T>` is never worse - use it,
  and the callback surface and the per-packet paths are where it pays. But a non-generic `async Task` that
  completes synchronously already hands back a cached `Task.CompletedTask`, so `ValueTask` there is **0 vs 0**
  and earns nothing. Return plain `Task` unless one of these applies:
  - it **forwards another `ValueTask`**, where `Task` would force an allocating `.AsTask()` - anything
    relaying `IAsyncDisposable.DisposeAsync()`, for instance;
  - it is an **interface contract on a repeated, frequently-suspending operation** that an implementer may
    want to back with a pooled `IValueTaskSource`. A `Task` is single-use, so every suspension allocates one,
    while an `IValueTaskSource` can be reused across operations on the same instance. This is why the BCL
    chose `ValueTask` for `DisposeAsync`, `Stream.ReadAsync` and `PipeWriter.FlushAsync`, and why
    `ILinkWriter` mirrors it.
- `CancellationToken` is the **last parameter** and is always passed on. Async iterators take `[EnumeratorCancellation] CancellationToken`.

### Performance mindset

Not rigid rules - lenses. When writing, reading or reviewing code here, actively look for these and **surface the opportunity even when not taking it**:

- **struct vs class**: is this copied on a hot path? Would `in` / `ref readonly` remove copies without mangling the call chain? Does `Span<T>`, `stackalloc` or `CollectionsMarshal` fit? Think past the obvious - array-of-structs vs struct-of-arrays.
- **`ValueTask` vs `Task`**: see the async rule above. `ValueTask<T>` saves an allocation whenever the result is known synchronously; non-generic `ValueTask` does not.
- **Allocation**: object pooling, `ArrayPool<T>`, closures captured in loops, boxing through interface calls on value types, `SearchValues`, frozen collections.
- **Readability wins.** No manual state machines, no avoiding LINQ where it is clearly right, no `unsafe` for marginal gains. If an optimization meaningfully hurts readability, say so and weigh the trade-off explicitly rather than silently skipping it.

### Tests

- **Arrange / Act / Assert**, always separated by blank lines - and by nothing else. `// Arrange` and its siblings only label what the blank lines already show, so do not write them. A comment block is always preceded by a blank line.
- Two projects, and the split decides where a test belongs. `Josupeit.Mail.Milter.UnitTests` needs no Docker and covers what a correct MTA can never send - malformed frames, protocol violations, byte-exact serialization, the abort and replay states. `Josupeit.Mail.Milter.IntegrationTests` drives a real Postfix through Testcontainers; see [CLAUDE.md](CLAUDE.md) for the fixtures.
- **Reach for the unit project first.** An integration run costs minutes and a Docker daemon; it earns that only when the point of the test is that Postfix agrees.
- **Bound every wait on a background worker.** A test that awaits a task which never completes hangs the whole run rather than failing one test, and NUnit's `CancelAfter` cannot break it. Race the wait against a timeout and assert on which won.

## Commit style

Commits follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/#specification): `<type>[optional scope]: <description>`, optionally followed by a body and footers.

- Common types here: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `chore`.
- The description is **third-person present** ("adds", "fixes", "moves"), lowercase, no trailing period.
- Write a body in most cases: the **reasoning** behind the change, not a restatement of the diff. Keep it brief - one or two sentences.
- Breaking changes: `!` after the type/scope and/or a `BREAKING CHANGE:` footer.
- No `Co-Authored-By` trailers.

```
feat(generator): adds source generator for protocol requirements

Deriving the bitmask from the overridden callbacks removes a manual step
that easily falls out of sync with the implementation.
```
