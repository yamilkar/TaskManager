# Collaborator rules

These rules apply to every contributor, human or AI. The PR reviewer checks against them.

## Architecture
- Follow Clean Architecture. Dependencies point inward: `Api → Application → Domain`, and `Infrastructure → Application`. Only `*.Api` (the composition root) references `*.Infrastructure`.
- Domain and Application reference no `Amazon.*` and no `Microsoft.AspNetCore.*` packages. NetArchTest tests enforce this; keep them green.
- Use **Controller → command/query handler → Repository** (CQRS style). Each use case is a command (changes state) or a query (read only) record with one sealed handler implementing `ICommandHandler<TCommand, TResponse>` or `IQueryHandler<TQuery, TResponse>` from SharedKernel. The record and its handler share one file in a per-use-case folder (`Register/`, `CreateTask/`), with a hand-written validator next to it when needed.
- Controllers are thin: they bind an Api-layer request DTO, build the command or query from it (with `userId` from the token), inject the handler interface per action with `[FromServices]`, and map `Result<T>` to HTTP. Handlers own business rules and return `Result<T>` with response DTOs. Repositories are interfaces in Application implemented in Infrastructure. Domain entities never leave the Application layer.
- Not allowed: Entity Framework, Dapper, MediatR, any mediator or dispatcher, and `*Service` classes in Application. Architecture tests enforce the handler rules.
- Users and Tasks share only `SharedKernel`. `userId` always comes from the JWT `sub` claim, never from the request.

## Testing
- Test-driven development: write a failing test (red), make it pass (green), then refactor. Every behavior starts as a test.
- Name tests `Method_condition_expected`, for example `RegisterAsync_duplicateEmail_returnsConflict`.
- Domain and Application line coverage must stay at 80% or higher (CI gate).

## Design principles
- **SOLID**: handlers depend on ports (`IUserRepository`, `IClock`), not implementations; each class has one reason to change.
- **DRY**: shared web plumbing lives once in `ApiCommon`, not copied per API.
- **KISS**: hand-written validators and mappers instead of heavy frameworks.
- **YAGNI**: no refresh tokens, pagination, mediator or separate read model until a story needs them.

## .NET conventions
- `<Nullable>enable</Nullable>` and warnings as errors; `dotnet build -warnaserror` must be clean.
- Return `Result<T>` for business failures; reserve exceptions for the unexpected.
- Never log passwords, hashes or tokens. Never commit secrets; use user-secrets locally.
- Target `net10.0`; package versions live in `Directory.Packages.props`.

## Angular conventions (https://angular.dev/style-guide)
- Standalone components, signals for state, `ChangeDetectionStrategy.OnPush`, `inject()`, `input()`/`output()`, `@if`/`@for`.
- No `.component` suffix in file names (`login.ts`, not `login.component.ts`).
- Organize by feature folders (`core/`, `layout/`, `features/`, `shared/`).
- Zero browser console warnings and errors; `ng lint --max-warnings 0` must pass.

## Git and PRs
- Conventional Commits (`feat:`, `fix:`, `docs:`, `ci:`, `test:`, `refactor:`), and one branch per story (`feature/story-<n>-<slug>`).
- **No symbolic links anywhere in the repository.** The `repo-hygiene` CI job fails on them.
- A PR needs green CI (`backend`, `frontend`, `repo-hygiene`).
- The AI reviewer only comments. It never approves, resolves threads or merges. A human resolves every thread and merges.
