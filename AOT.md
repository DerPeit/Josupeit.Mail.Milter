# Native AOT Compatibility

The library is **Native AOT compatible**. On its `net8.0` target it is marked
`<IsAotCompatible>true</IsAotCompatible>`, which turns on the full trim / AOT / single-file analyzer
suite at build time, so AOT-hostile code is flagged during compilation rather than failing at publish.

## What "AOT-compatible" means here

- **The library builds analyzer-clean.** A Release build produces zero `IL` (trim/AOT/single-file)
  warnings on the `net8.0` target.
- **A real native binary using the library runs.** A smoke-test app is published with
  `PublishAot=true` and executed in CI on every push (see below).
- **One convenience path is quarantined.** The `Milter<T>` constructors that build the context through
  `Activator.CreateInstance` (i.e. the ones without a `Func<TContext>` factory) carry
  `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`. An AOT consumer who calls them sees a
  build warning pointing at the factory overloads, which are completely clean.

It does **not** mean your application is automatically AOT-compatible. If your own code uses
reflection, dynamic code generation, or reflection-based serialization, that remains your
responsibility. This library will not be the thing that breaks your AOT publish.

## How it's verified

The repository ships a Native AOT regression fixture at `aot/Josupeit.Mail.Milter.AotSmoke/`. It is
**not** a usage example: it is a CI gate.

`Josupeit.Mail.Milter.AotSmoke.csproj` sets:

```xml
<PublishAot>true</PublishAot>
<IlcTreatWarningsAsErrors>true</IlcTreatWarningsAsErrors>
<TrimmerSingleWarn>false</TrimmerSingleWarn>
```

so any trim/AOT `IL` warning in a reachable code path **fails the publish**, and each warning is
reported individually rather than collapsed. The CI `aot` job (`.forgejo/workflows/ci.yml`):

1. Installs the native toolchain (`clang`, `zlib1g-dev`).
2. Publishes `AotSmoke` as a native binary (`dotnet publish --runtime linux-x64`).
3. Runs the native binary and checks it prints `AOT SMOKE OK`.

If any step fails, CI fails.

### What the smoke test exercises

- **Commit actions**: every singleton (`Continue`, `Accept`, `Reject`, `Discard`, `Defer`) and the
  parameterised `Reply`.
- **Modification actions**: every factory (`AddHeader`, `InsertHeader`, `ChangeHeader`, `DeleteHeader`,
  `AddRecipient` v2 and v6, `DeleteRecipient`, `ChangeSender`, `QuarantineMessage`, `ReplaceBody`).
- **`Milter<T>` construction** through the factory path (the AOT-safe one), with and without options.
- **The source generator**: the smoke context's `ProtocolRequirements` and `ActionRequirements` are
  generator-derived, so the generated code compiles and links natively.

## Tips for AOT-publishing your own filter

- Use the `Milter<T>(Socket, Func<TContext>)` constructor (or the overload with `MilterOptions`).
  The parameterless-factory constructors use `Activator.CreateInstance` and will warn.
- Set `<IsAotCompatible>true</IsAotCompatible>` in your own project to get the analyzers, then fix
  warnings as they appear.
- Test with an actual `dotnet publish -p:PublishAot=true` run, not just a build. Some issues only
  surface during native compilation.
