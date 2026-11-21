# AStarDev mono-repo

.NET 11, C#, solution `AStarDev.slnx`. Apps in `apps/`, shared packages in `nuget-packages/`.
Architecture docs in `docs/architecture/` are the source of truth; start at `00 architecture-index.md`. On contradiction between code and docs, ask before changing either.

## Build and test

- Build: `dotnet build` (must finish 0 errors, 0 warnings).
- Tests use xunit v3 on Microsoft.Testing.Platform. `dotnet test` from the repo root can fail with `Unknown option '--report-trx'`; run the test assemblies directly:
  `dotnet exec <TestsProject>/bin/Debug/net11.0/<TestsProject>.dll`
- Run both `*TestsUnit` and `AStarDev.CloudSync.TestsArchitecture` before finishing. Architecture tests enforce public surface and dependency rules (see `docs/architecture/15`).
- Git: conventional commits (`feat(scope): subject`, imperative, lowercase, no trailing period, subject <= 50 chars). See `.github/CONTRIBUTING.md`.

## Formatting and style (enforced by `.editorconfig`; keep new code identical)

- File-scoped namespaces. `using` directives outside the namespace, `System.*` first. Implicit usings and nullable are enabled.
- Allman braces. 4-space indent in `.cs`, 2-space in project/config files. LF line endings, final newline.
- One blank line before a `return` that follows other statements. Prefer expression-bodied members for single expressions.
- `var` everywhere the type is apparent or irrelevant.
- Primary constructors for classes that only capture dependencies; captured parameters are used directly.
- Private/internal fields: camelCase, NO underscore prefix, NO `this.` (`commands`, `lifetime`, not `_commands`). Constants: PascalCase. Interfaces: `I` prefix.
- One type per file, named after the type. Exception: a union and every case type of that union live together in the union's file (see `Result.cs`, `Exceptional{T}.cs`, `IpcError.cs`, `ProviderError.cs`). Declare the cases first, the `union` last.
- Types are `internal sealed` unless they are part of the module's approved public surface. Adding a public type means updating `PublicSurfaceArchitectureTestsShouldEnsure`.

## Comments

- No `//` comments. If code needs explaining, rename or extract. Do not add comments when editing; never delete existing ones unless asked.
- XML doc comments (`///`) on every public type and member: a one-line `<summary>`, `<param>`/`<typeparam>` where they add meaning. Say what it is or why it exists, not how.
- Do not add banners, region blocks, or commented-out code.

## Error handling

- Failable methods return `Result<T, TError>` (or `Option`/`Try`) from `AStarDev.FunctionalParadigm`. No exceptions for expected failures, no `try/catch` in application code. Wrap code that can throw in `Try.Run` / `Try.RunAsync` and convert with `ToResult`.
- Error types are closed unions of records (`IpcError`, `ProviderError`, `StartupError`). Match them exhaustively with `switch`, no `_` default.
- `Domain` must not reference `FunctionalParadigm`; any other module may.
- Cancellation is not a failure: `OperationCanceledException` propagates.

## Async

- Every async method takes `CancellationToken cancellationToken` as its LAST parameter, passes it to everything it awaits, honours it, and has a test proving cancellation.
- No `.Result`, `.Wait()`. Use `ConfigureAwait(false)` in library code (`FunctionalParadigm`).

## Tests

- One test project per module, `<Module>.TestsUnit`; architecture rules only in `AStarDev.CloudSync.TestsArchitecture`. Do not mix them.
- Test class: `<Subject>Should`, sealed, public. Test method: snake_case sentence completing "Subject should ..." (`return_a_failure_for_a_frame_that_is_not_an_envelope`).
- xunit v3 + Shouldly only. Use `TestContext.Current.CancellationToken`, never `CancellationToken.None`, in tests.
- Union values are structs: assert on `.Value` (`result.Value.ShouldBeOfType<Ok<int>>()`), and use `ShouldBe`, not `ShouldBeSameAs`.
- New behaviour: failing test first.

## Before finishing a change

1. `dotnet build` clean.
2. All test assemblies pass (command above).
3. No `//` comments, no underscore fields, no stray files (`Class1.cs` placeholders are deleted when a module gets real types; update the architecture placeholder list when that happens).
