# AStarDev mono-repo

.NET 11, C#, solution `AStarDev.slnx`. Apps in `apps/`, shared packages in `nuget-packages/`.
Architecture docs in `docs/architecture/` = source of truth; start at `00 architecture-index.md`. Code and docs contradict: ask before changing either.

## Build and test

- Build: `dotnet build` (must finish 0 errors, 0 warnings).
- Tests use xunit v3 on Microsoft.Testing.Platform. `dotnet test` from repo root can fail with `Unknown option '--report-trx'`; run test assemblies directly:
  `dotnet exec <TestsProject>/bin/Debug/net11.0/<TestsProject>.dll`
- Run both `*TestsUnit` and `AStarDev.CloudSync.TestsArchitecture` before finishing. Architecture tests enforce public surface and dependency rules (see `docs/architecture/15`).
- Never call `[Obsolete]` APIs, never suppress warnings (`CS0618`, `ASPIRE*`): switch to replacement warning names. Check test projects too, including `*TestsIntegration`.
- Git: conventional commits (`feat(scope): subject`, imperative, lowercase, no trailing period, subject <= 50 chars). See `.github/CONTRIBUTING.md`.

## Formatting and style (enforced by `.editorconfig`; keep new code identical)

- File-scoped namespaces. `using` directives outside namespace, `System.*` first. Implicit usings and nullable enabled.
- Allman braces. 4-space indent in `.cs`, 2-space in project/config files. LF line endings, final newline.
- No braces around single-line body: `if (items.Count == 0) break;`, and `foreach` with one statement on next line without braces. Braces only when body spans more than one line.
- Keep call's argument list on one line, even when long (`new ProviderItem(id, path, type, size, modified)`). Break lines only between calls of fluent chain.
- Always write access modifier, including `private` on members and helpers (`private static`, never bare `static`).
- One blank line before `return` that follows other statements. Prefer expression-bodied members for single expressions.
- `var` everywhere type apparent or irrelevant.
- Primary constructors for classes that only capture dependencies; use captured parameters directly.
- Private/internal fields: camelCase, NO underscore prefix, NO `this.` (`commands`, `lifetime`, not `_commands`). Constants: PascalCase. Interfaces: `I` prefix.
- One type per file, named after type. Exception: union and every case type of that union live together in union's file (see `Result.cs`, `Exceptional{T}.cs`, `IpcError.cs`, `ProviderError.cs`). Declare cases first, `union` last.
- Types `internal sealed` unless part of module's approved public surface. Adding public type means updating `PublicSurfaceArchitectureTestsShouldEnsure`.

## Comments

- No `//` comments. Code needs explaining: rename or extract. Do not add comments when editing; never delete existing ones unless asked.
- XML doc comments (`///`) on every public type and member: one-line `<summary>`, `<param>`/`<typeparam>` where add meaning. Say what it is or why exists, not how.
  - "Public" = every member written `public`, including members of `internal` types, interface members, enum values, records, union case types. Only private members may go undocumented.
  - Docs must be meaningful. Never restate name ("Gets the socket path"). State behaviour caller cannot see from signature: when fails and which error case, defaults, ordering, what `null` or empty means, what is safe to call twice.
  - Use `<returns>` to list failure cases of `Result`. Add `<example><code>` for entry points and APIs easy to misuse (see `IIpcClient`, `DaemonStatus.cs`).
  - Interface implementations use `/// <inheritdoc/>`. Add `<remarks>` only where implementation differs from contract.
  - Exactly one `<summary>` per member. Every `cref` must resolve from module it is in (Domain cannot `cref` Ports types; use `<c>` instead).
  - Check before finishing: `dotnet build -p:GenerateDocumentationFile=true` must report no `CS1591`, `CS1572`, `CS1573` or `CS1574` in production projects. Does not catch `public` members of `internal` types, so check those by reading code.
- No banners, region blocks, commented-out code.

## Error handling

- Failable methods return `Result<T, TError>` (or `Option`/`Try`) from `AStarDev.FunctionalParadigm`. No exceptions for expected failures, no `try/catch` in application code. Wrap throwing code in `Try.Run` / `Try.RunAsync`, convert with `ToResult`.
- Error types = closed unions of records (`IpcError`, `ProviderError`, `StartupError`). Match exhaustively with `switch`, no `_` default.
- `Domain` must not reference `FunctionalParadigm`; any other module may.
- Cancellation not failure: `OperationCanceledException` propagates.

## Async

- Every async method takes `CancellationToken cancellationToken` as LAST parameter, passes it to everything awaited, honours it, has test proving cancellation.
- No `.Result`, `.Wait()`. Use `ConfigureAwait(false)` in library code (`FunctionalParadigm`).

## Composition

- Each app's whole service graph lives in one `internal static` extension method in app project (`DaemonServiceCollectionExtensions.AddCloudSyncDaemon`). `Program.cs` only creates builder, calls that method, runs host. Never register services inline in `Program.cs`.
- New services, options, hosted services registered inside that method, in order they must start.

## Tests

- Tests needing DI resolve from real composition: build host with app's `Add...` method (see `DaemonTestHost`), feed test values through configuration. Never hand-build `ServiceCollection` or register partial graph in test. Replace only what doc 14 allows (Persistence, Provider, Telemetry, Time), after calling real method.
- Cover wiring itself with `<Extensions>Should` class against real host: registration order, options binding, app answering end to end.
- Unit test of one class may construct it directly with fakes (`FakeStage`, `FakeIpcServer`) when needs failing or recording collaborator real wiring cannot give.
- One test project per module, `<Module>.TestsUnit`; architecture rules only in `AStarDev.CloudSync.TestsArchitecture`. Do not mix.
- Test class: `<Subject>Should`, sealed, public. Test method: snake_case sentence completing "Subject should ..." (`return_a_failure_for_a_frame_that_is_not_an_envelope`).
- xunit v3 + Shouldly only. Use `TestContext.Current.CancellationToken`, never `CancellationToken.None`, in tests.
- Union values are structs: assert on `.Value` (`result.Value.ShouldBeOfType<Ok<int>>()`), use `ShouldBe`, not `ShouldBeSameAs`.
- New behaviour: failing test first.

## Before finishing a change

1. `dotnet build` clean.
2. All test assemblies pass (command above).
3. XML doc check above clean; DI tests go through real composition.
4. No `//` comments, no underscore fields, no stray files (`Class1.cs` placeholders deleted when module gets real types; update architecture placeholder list then).