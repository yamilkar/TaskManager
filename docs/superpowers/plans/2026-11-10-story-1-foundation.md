# Foundation + Story 1 (Sign in and add my first task) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Users API, Tasks API, DynamoDB data layer, Angular client, CI and docs so a user can register or sign in (demo account seeded) and add and see their own tasks. This covers issue #15.

**Architecture:** There are two independent ASP.NET Core (.NET 10) APIs. Each uses Clean Architecture layers (Domain → Application → Infrastructure → Api) and the Controller → Service → Repository pattern with explicit DTOs. Persistence is DynamoDB through the low-level AWS SDK, with hand-written mappers. The Users API issues HS256 JWTs and the Tasks API validates them. The Angular client uses signal stores, functional interceptors and guards, and loads `config.json` at runtime.

**Tech Stack:**
- **Backend:** .NET 10, ASP.NET Core controllers, AWSSDK.DynamoDBv2, Amazon.Lambda.AspNetCoreServer.Hosting, Microsoft.Extensions.Identity.Core (PasswordHasher), Microsoft.AspNetCore.Authentication.JwtBearer, Serilog.AspNetCore, Amazon.CloudWatch.EMF, Scalar.AspNetCore.
- **Backend tests:** xUnit, NSubstitute, Shouldly, Testcontainers.DynamoDb, NetArchTest.Rules, coverlet.
- **Frontend:** latest stable Angular CLI with Angular Material (M3), Vitest and angular-eslint.

**Spec:** `docs/superpowers/specs/2026-11-10-story-1-foundation-design.md`

## Global Constraints

- **Target and build settings:** target `net10.0`; `<Nullable>enable</Nullable>`; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`; central package versions in `Directory.Packages.props`.
- **Banned:** Entity Framework, Dapper, MediatR, and any handler-per-use-case or mediator pattern. Use Controller → Service → Repository.
- **Dependency rule:** Domain and Application reference no `Amazon.*` and no `Microsoft.AspNetCore.*` packages. Only `*.Api` references `*.Infrastructure`.
- **Table names:** `TaskManager-Users` (pk `pk`) and `TaskManager-Tasks` (pk `userId`, sk `_id`).
- **DynamoDB attribute names** (verbatim): `_id`, `userId`, `title`, `description`, `status`, `due_date`, `createdAt`, `updatedAt`, `full_name`, `email`, `passwordHash`.
- **Key prefixes:** user items use `USER#<guid>` and email guard items use `EMAIL#<normalized email>`.
- **Field rules:**
  - title 1–200 (trimmed); description ≤ 2000 (trimmed, empty → null)
  - full_name 1–100 (trimmed); email ≤ 254, valid format, trim + lower-invariant
  - password 8–128 with at least one letter and one digit
  - status `ToDo|InProgress|Done` (default `ToDo`)
  - due_date `yyyy-MM-dd`, not before today (UTC) at creation
- **JWT:** HS256; issuer `taskmanager-users`; audience `taskmanager-api`; lifetime 60 min; claims `sub`, `email`, `name`; clock skew 30 s. The signing key comes from `Jwt:SigningKey` and must be ≥ 32 bytes or the app fails at startup.
- **Login:** a wrong email and a wrong password both return `401` with detail `Invalid email or password.`. Login is rate-limited to 5 requests per minute per IP, returning `429`.
- **Demo account:** `demo@taskmanager.dev` / `Demo123!`, full name `Demo User`, fixed id `11111111-1111-1111-1111-111111111111`.
- **Local ports:** Users API `http://localhost:5080`, Tasks API `http://localhost:5090`, Angular `http://localhost:4200`, DynamoDB Local `http://localhost:8000`.
- **Never log** passwords, hashes, or tokens.
- **No symbolic links** anywhere in the repo.
- **Commits:** Conventional Commits, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Work on branch `feature/story-1-sign-in-add-task`.
- **Coverage:** Domain and Application projects need ≥ 80% line coverage.
- **Browser console:** zero warnings and zero errors.

## Review Focus

1. **Email case and whitespace:** registering ` Ana@X.com ` and logging in with `ana@x.com` must reach the same account, and registering `ANA@x.com` afterwards must return 409. Tests: Task 3 (`Email`) and Task 4 (`UserService`).
2. **Cross-user access:** a valid token for user A requesting user B's task id must get `404`, never B's data. Tests: Task 10 (service) and Task 12 (API).
3. **Due date today at the UTC boundary:** a due date equal to today's UTC date is accepted, and yesterday is rejected. Test: Task 9.
4. **Malformed input:** a malformed JSON body, a missing body, or an unknown `status` string must return `400` ProblemDetails, never `500`. Tests: Task 8 and Task 12.
5. **Login 401 versus session expiry:** a `401` from the login endpoint must show "Invalid email or password." and must **not** trigger the "session expired" redirect. A `401` from the Tasks API must trigger it. Test: Task 14 (`errorInterceptor`).

---

## File Structure

```
TaskManager.slnx
Directory.Build.props · Directory.Packages.props · global.json · .editorconfig · .gitattributes · .gitignore
docker-compose.yml
src/BuildingBlocks/
  TaskManager.SharedKernel/            Result.cs, Error.cs, ErrorType.cs, IClock.cs, SystemClock.cs, IAppMetrics.cs, Security/JwtOptions.cs
  TaskManager.Infrastructure.DynamoDb/ DynamoDbOptions.cs, DynamoDbServiceCollectionExtensions.cs, DynamoTableHealthCheck.cs, TableBootstrapper.cs, AttributeValueExtensions.cs
  TaskManager.ApiCommon/               ApiControllerBase.cs, GlobalExceptionHandler.cs, CorrelationIdMiddleware.cs, EmfAppMetrics.cs,
                                       ApiDefaultsExtensions.cs (services + pipeline), JwtBearerSetup.cs
src/Users/
  TaskManager.Users.Domain/            Email.cs, User.cs
  TaskManager.Users.Application/       Contracts/*.cs, Validation/*.cs, Ports/*.cs, UserErrors.cs, UsersMetrics.cs, IUserService.cs, UserService.cs, DependencyInjection.cs
  TaskManager.Users.Infrastructure/    Persistence/UserItemMapper.cs, Persistence/DynamoDbUserRepository.cs, Persistence/UsersTableDefinition.cs,
                                       Security/AspNetPasswordHasher.cs, Security/JwtTokenIssuer.cs, Seeding/UserSeedOptions.cs, Seeding/UserSeeder.cs,
                                       UsersDatabaseBootstrapper.cs, DependencyInjection.cs
  TaskManager.Users.Api/               Program.cs, Controllers/AuthController.cs, Controllers/UsersController.cs, appsettings*.json, Properties/launchSettings.json
src/Tasks/  (mirror) Domain: TaskItem.cs, TaskItemStatus.cs · Application: Contracts, Validation, Ports, TaskErrors, TasksMetrics, ITaskService, TaskService
            Infrastructure: Persistence/TaskItemMapper.cs, DynamoDbTaskRepository.cs, TasksTableDefinition.cs, Seeding/TaskSeeder.cs, TasksDatabaseBootstrapper.cs
            Api: Program.cs, Controllers/TasksController.cs
tests/
  TaskManager.Testing/                 FixedClock.cs, TestJwt.cs, DynamoDbLocalFixture.cs (shared test helpers)
  TaskManager.SharedKernel.Tests/ · TaskManager.ApiCommon.Tests/
  TaskManager.Users.{Domain,Application,Infrastructure,Api}.Tests/
  TaskManager.Tasks.{Domain,Application,Infrastructure,Api}.Tests/
  TaskManager.ArchitectureTests/
web/  (Angular; see Tasks 13–15)
.github/workflows/ci.yml · .github/workflows/pr-review.yml
.claude/rules/rules.md · README.md · GENAI.md
```

> **Refinement of spec §3.4 (flag for human review):** two extra building-block projects keep the APIs DRY without coupling their domains. `TaskManager.ApiCommon` holds web-host plumbing (ProblemDetails mapping, exception handler, JWT bearer, CORS, Serilog, EMF, health endpoints). `TaskManager.Infrastructure.DynamoDb` holds the client registration, table bootstrap and health check. Neither contains Users or Tasks domain code.

---

### Task 1: Repository scaffold and architecture tests

**Files:**
- Create: `global.json` (sdk `10.0.401`, rollForward `latestFeature`), `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitattributes` (`* text=auto eol=lf`), `.gitignore` (dotnet + node + `.angular/`), `TaskManager.slnx`
- Create: every `src/**` and `tests/**` `.csproj` listed in File Structure, with references:
  - Domain → SharedKernel
  - Application → Domain, SharedKernel
  - Infrastructure → Application, Infrastructure.DynamoDb
  - Api → Application, Infrastructure, ApiCommon
  - Each test project → its subject + TaskManager.Testing
- Create: `docker-compose.yml`, with `dynamodb-local` (`amazon/dynamodb-local:2.5.3`, `-jar DynamoDBLocal.jar -sharedDb -inMemory`, port 8000) and `dynamodb-admin` (`aaronshaf/dynamodb-admin:4.6.1`, port 8001, `DYNAMO_ENDPOINT=http://dynamodb-local:8000`)
- Test: `tests/TaskManager.ArchitectureTests/DependencyRuleTests.cs`

**Interfaces:**
- Produces: the solution skeleton every later task adds to. `Api` projects use `Microsoft.NET.Sdk.Web`; all others use `Microsoft.NET.Sdk`.

- [ ] **Step 1: Create the solution, projects and references**

Run: `dotnet new sln --format slnx -n TaskManager`, then `dotnet new classlib|webapi|xunit` for each project and `dotnet sln add`. Delete template `Class1.cs` / `UnitTest1.cs` / `WeatherForecast*`. Pin every package version in `Directory.Packages.props` (latest stable at execution time) and record the versions in the commit message.

- [ ] **Step 2: Write the failing architecture tests**

Each test loads its assembly through a marker type that later tasks create (`TaskManager.Users.Domain.User`, etc.). Add a temporary `AssemblyMarker` class to each project now so the tests compile.

```csharp
[Theory]
[InlineData("TaskManager.Users.Domain"), InlineData("TaskManager.Users.Application"),
 InlineData("TaskManager.Tasks.Domain"), InlineData("TaskManager.Tasks.Application"),
 InlineData("TaskManager.SharedKernel")]
public void Core_layers_do_not_depend_on_aws_or_aspnet(string assembly) =>
    Types.InAssembly(Load(assembly)).ShouldNot()
        .HaveDependencyOnAny("Amazon", "Microsoft.AspNetCore", "TaskManager.ApiCommon", "TaskManager.Infrastructure.DynamoDb")
        .GetResult().IsSuccessful.ShouldBeTrue();

[Theory] InlineData("Users"), InlineData("Tasks")
public void Domain_does_not_depend_on_application_or_infrastructure(string ctx) ...   // .HaveDependencyOnAny($"TaskManager.{ctx}.Application", $"TaskManager.{ctx}.Infrastructure", $"TaskManager.{ctx}.Api")
public void Application_does_not_depend_on_infrastructure_or_api(string ctx) ...
public void Users_and_Tasks_do_not_reference_each_other() ...                      // no TaskManager.Tasks.* in Users assemblies and vice versa
```

- [ ] **Step 3: Run tests and verify they fail, then pass after the reference fixes**

Run: `dotnet test tests/TaskManager.ArchitectureTests`
Expected: they fail first if any reference is wrong. Fix the references until the run shows PASS (8+ tests).

- [ ] **Step 4: Verify the whole solution builds warning-free**

Run: `dotnet build TaskManager.slnx -warnaserror`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Commit**

`git add -A && git commit -m "chore: scaffold solution, projects and architecture tests"`

---

### Task 2: SharedKernel (Result, Error, clock, metrics port, JWT options)

**Files:**
- Create: `src/BuildingBlocks/TaskManager.SharedKernel/{ErrorType,Error,Result,IClock,SystemClock,IAppMetrics}.cs`, `Security/JwtOptions.cs`
- Create: `tests/TaskManager.Testing/FixedClock.cs`
- Test: `tests/TaskManager.SharedKernel.Tests/{ResultTests,JwtOptionsTests}.cs`

**Interfaces:**
- Produces:
  - `enum ErrorType { Validation, NotFound, Conflict, Unauthorized }`
  - `sealed record Error(string Code, string Message, ErrorType Type, IReadOnlyDictionary<string,string[]>? FieldErrors = null)` with static factories `Validation(string code, string message, IReadOnlyDictionary<string,string[]> fieldErrors)`, `NotFound(code, message)`, `Conflict(code, message)`, `Unauthorized(code, message)`
  - `class Result { bool IsSuccess; bool IsFailure; Error? Error; static Result Success(); static Result Failure(Error) }`
  - `sealed class Result<T> : Result { T Value /* throws InvalidOperationException on failure */; static Result<T> Success(T); static new Result<T> Failure(Error); implicit operator Result<T>(T); implicit operator Result<T>(Error) }`
  - `interface IClock { DateTimeOffset UtcNow { get; } }`; `sealed class SystemClock : IClock`
  - `interface IAppMetrics { void Increment(string metricName); }`
  - `sealed class JwtOptions { const string SectionName = "Jwt"; string Issuer = "taskmanager-users"; string Audience = "taskmanager-api"; string SigningKey = ""; int LifetimeMinutes = 60; void EnsureValid() /* throws InvalidOperationException when UTF-8 key bytes < 32 */ }`
  - Test helper: `sealed class FixedClock(DateTimeOffset now) : IClock`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Success_result_exposes_value() { Result<int> r = 5; r.IsSuccess.ShouldBeTrue(); r.Value.ShouldBe(5); }
[Fact] public void Failure_result_exposes_error_and_throws_on_value() {
    Result<int> r = Error.Conflict("x.conflict", "msg");
    r.IsFailure.ShouldBeTrue(); r.Error!.Type.ShouldBe(ErrorType.Conflict);
    Should.Throw<InvalidOperationException>(() => _ = r.Value); }
[Fact] public void Validation_error_carries_field_errors() { ... FieldErrors!["title"].ShouldBe(new[] { "Title is required." }); }
[Fact] public void JwtOptions_rejects_key_shorter_than_32_bytes() => Should.Throw<InvalidOperationException>(() => new JwtOptions { SigningKey = new string('k', 31) }.EnsureValid());
[Fact] public void JwtOptions_accepts_32_byte_key() => Should.NotThrow(() => new JwtOptions { SigningKey = new string('k', 32) }.EnsureValid());
```

- [ ] **Step 2: Run and confirm FAIL** (`dotnet test tests/TaskManager.SharedKernel.Tests`; it fails to compile because the types are missing)
- [ ] **Step 3: Implement the types listed in Interfaces**
- [ ] **Step 4: Run and confirm PASS**
- [ ] **Step 5: Commit** with `feat(shared): add Result, Error, clock, metrics port and JWT options`

---

### Task 3: Users domain (Email value object, User entity)

**Files:**
- Create: `src/Users/TaskManager.Users.Domain/{Email,User}.cs` (delete `AssemblyMarker`)
- Test: `tests/TaskManager.Users.Domain.Tests/{EmailTests,UserTests}.cs`

**Interfaces:**
- Consumes: `Result<T>`, `Error` (Task 2)
- Produces:
  - `sealed record Email { string Value { get; } static Result<Email> Create(string? raw); }`
    - Normalization: trim, then `ToLowerInvariant`. Errors use field key `email`, code `users.email_invalid`, message `Enter a valid email address.`
    - Format check: a regex for `^[^@\s]+@[^@\s]+\.[^@\s]+$`, with length ≤ 254.
  - `sealed class User { Guid Id; string FullName; Email Email; string PasswordHash; DateTimeOffset CreatedAt; static Result<User> Create(Guid id, string? fullName, Email email, string passwordHash, DateTimeOffset createdAt); static User Rehydrate(Guid id, string fullName, Email email, string passwordHash, DateTimeOffset createdAt); }`
    - `fullName` is trimmed and must be 1–100 characters, using field key `fullName`. The error messages are `Full name is required.` and `Full name must be at most 100 characters.`

- [ ] **Step 1: Write failing tests**

```csharp
[Theory, InlineData(" Ana@X.com ", "ana@x.com"), InlineData("BOB@EXAMPLE.ORG", "bob@example.org")]
public void Create_normalizes(string raw, string expected) => Email.Create(raw).Value.Value.ShouldBe(expected);
[Theory, InlineData(null), InlineData(""), InlineData("   "), InlineData("no-at-sign"), InlineData("a@b"), InlineData("a b@c.com")]
public void Create_rejects_invalid(string? raw) => Email.Create(raw).Error!.FieldErrors!.ShouldContainKey("email");
[Fact] public void Create_rejects_longer_than_254() => Email.Create(new string('a', 245) + "@test.com").IsFailure.ShouldBeTrue();   // 254 chars passes? 245+9=254 → passes; use 246 for failure
[Fact] public void User_Create_trims_full_name() => User.Create(id, "  Ana  ", email, "hash", now).Value.FullName.ShouldBe("Ana");
[Theory, InlineData(""), InlineData("   ")] public void User_Create_requires_full_name(string n) => ...FieldErrors!["fullName"].ShouldContain("Full name is required.");
[Fact] public void User_Create_rejects_101_char_name() => ...
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement `Email` and `User`**
- [ ] **Step 4: Run and confirm PASS** with `dotnet test tests/TaskManager.Users.Domain.Tests`
- [ ] **Step 5: Commit** with `feat(users): add Email value object and User entity`

---

### Task 4: Users application layer (DTOs, validators, ports, UserService)

**Files:**
- Create in `src/Users/TaskManager.Users.Application/`:
  - `Contracts/{RegisterUserRequest,LoginRequest,UserResponse,AuthResponse}.cs`
  - `Validation/{RegisterUserRequestValidator,LoginRequestValidator}.cs`
  - `Ports/{IUserRepository,IPasswordHasher,ITokenIssuer,IssuedToken}.cs`
  - `UserErrors.cs`, `UsersMetrics.cs`, `IUserService.cs`, `UserService.cs`, `DependencyInjection.cs`
- Test: `tests/TaskManager.Users.Application.Tests/{RegisterUserRequestValidatorTests,UserServiceRegisterTests,UserServiceLoginTests,UserServiceGetByIdTests}.cs`

**Interfaces:**
- Consumes: `Email`, `User` (Task 3); `Result<T>`, `Error`, `IClock`, `IAppMetrics` (Task 2)
- Produces:
  - **DTOs:**
    - `record RegisterUserRequest(string? FullName, string? Email, string? Password)`
    - `record LoginRequest(string? Email, string? Password)`
    - `record UserResponse(Guid Id, string FullName, string Email, DateTimeOffset CreatedAt)`
    - `record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User)`
  - **Ports:**
    - `interface IUserRepository { Task<bool> TryAddAsync(User user, CancellationToken ct); Task<User?> GetByEmailAsync(Email email, CancellationToken ct); Task<User?> GetByIdAsync(Guid id, CancellationToken ct); }`. `TryAddAsync` returns false when the email is already registered.
    - `interface IPasswordHasher { string Hash(string password); bool Verify(string? passwordHash, string password); }`. When `passwordHash` is null, the hasher verifies against an internal dummy hash and returns false, which equalizes timing.
    - `interface ITokenIssuer { IssuedToken Issue(User user); }`; `record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt)`
  - **Validators:** `static Error? RegisterUserRequestValidator.Validate(RegisterUserRequest r)` and `LoginRequestValidator.Validate(LoginRequest r)`. They return a combined `Error.Validation("validation_failed", "One or more validation errors occurred.", fields)`, or null.
    - Password rules use field key `password`, with messages `Password must be at least 8 characters.`, `Password must be at most 128 characters.` and `Password must contain at least one letter and one digit.`
    - Login requires a non-empty `email` and `password`, each with the message `{Field} is required.`
  - **`UserErrors`:**
    - `EmailAlreadyRegistered = Error.Conflict("users.email_taken", "An account with this email already exists.")`
    - `InvalidCredentials = Error.Unauthorized("auth.invalid_credentials", "Invalid email or password.")`
    - `NotFound = Error.NotFound("users.not_found", "User not found.")`
  - **`UsersMetrics`:** constants `UserRegistered`, `LoginSucceeded`, `LoginFailed`
  - **Service:** `interface IUserService { Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct); Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct); Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct); }`
  - **DI:** `IServiceCollection AddUsersApplication(this IServiceCollection)` registers `IUserService` as scoped.

- [ ] **Step 1: Write failing tests** (NSubstitute for the ports, `FixedClock` at `2026-11-10T12:00:00Z`)

```csharp
// Validator
[Theory, InlineData("short1"), InlineData("allletters"), InlineData("12345678")] Register_rejects_weak_password(string pw) → FieldErrors["password"] not empty
[Fact] Register_collects_errors_for_all_invalid_fields() → keys contain "fullName","email","password"
// Register
[Fact] RegisterAsync_hashes_password_and_returns_user_with_normalized_email()
    → repo.TryAddAsync(Arg.Is<User>(u => u.Email.Value=="ana@x.com" && u.PasswordHash=="HASHED"), ...); result.Value.Email == "ana@x.com"; CreatedAt == clock.UtcNow
[Fact] RegisterAsync_returns_conflict_when_repository_reports_existing_email()  → repo.TryAddAsync returns false → Error == UserErrors.EmailAlreadyRegistered
[Fact] RegisterAsync_does_not_touch_repository_when_invalid()                    → repo.DidNotReceiveWithAnyArgs().TryAddAsync(...)
[Fact] RegisterAsync_increments_UserRegistered_metric_on_success()
// Login
[Fact] LoginAsync_returns_token_for_valid_credentials_using_normalized_email()   → login with " ANA@x.com "; repo.GetByEmailAsync(Arg.Is<Email>(e => e.Value=="ana@x.com"))
[Fact] LoginAsync_returns_InvalidCredentials_for_wrong_password()
[Fact] LoginAsync_returns_InvalidCredentials_for_unknown_email_and_still_verifies_dummy() → hasher.Received(1).Verify(null, "Secret123")
[Fact] LoginAsync_returns_InvalidCredentials_for_malformed_email()             → not a Validation error, so the API does not reveal which part was wrong
[Fact] LoginAsync_increments_LoginFailed_on_failure_and_LoginSucceeded_on_success()
// GetById
[Fact] GetByIdAsync_returns_NotFound_when_missing()
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement validators, `UserErrors`, `UserService`, `AddUsersApplication`** (`UserService` maps `User → UserResponse` with a private static `ToResponse`)
- [ ] **Step 4: Run and confirm PASS** with `dotnet test tests/TaskManager.Users.Application.Tests`
- [ ] **Step 5: Commit** with `feat(users): add user service with registration and login rules`

---

### Task 5: DynamoDB building block + Users persistence

**Files:**
- Create: `src/BuildingBlocks/TaskManager.Infrastructure.DynamoDb/{DynamoDbOptions,DynamoDbServiceCollectionExtensions,TableBootstrapper,DynamoTableHealthCheck,AttributeValueExtensions}.cs`
- Create: `src/Users/TaskManager.Users.Infrastructure/Persistence/{UsersTableDefinition,UserItemMapper,DynamoDbUserRepository}.cs`
- Create: `tests/TaskManager.Testing/DynamoDbLocalFixture.cs`
- Test: `tests/TaskManager.Users.Infrastructure.Tests/{UserItemMapperTests,DynamoDbUserRepositoryTests}.cs`

**Interfaces:**
- Consumes: `IUserRepository`, `User`, `Email` (Tasks 3–4)
- Produces:
  - `sealed class DynamoDbOptions { const string SectionName = "Dynamo"; string? ServiceUrl; string Region = "us-east-1"; string TableName = ""; bool CreateTablesIfMissing; }`
  - `IServiceCollection AddDynamoDb(this IServiceCollection, IConfiguration)` registers a singleton `IAmazonDynamoDB`. When `ServiceUrl` is set, it uses that URL with `BasicAWSCredentials("local","local")`; otherwise it uses the default credential chain.
  - `static Task TableBootstrapper.EnsureTableAsync(IAmazonDynamoDB db, CreateTableRequest request, CancellationToken ct)` creates the table if `DescribeTable` throws `ResourceNotFoundException`, then waits until it is `ACTIVE`.
  - `sealed class DynamoTableHealthCheck(IAmazonDynamoDB db, IOptions<DynamoDbOptions> options) : IHealthCheck` reports Healthy when `DescribeTable(TableName)` succeeds.
  - `UsersTableDefinition.Create(string tableName) → CreateTableRequest` (pk `pk` S, `PAY_PER_REQUEST`)
  - `static class UserItemMapper { Dictionary<string,AttributeValue> ToUserItem(User); Dictionary<string,AttributeValue> ToEmailGuardItem(User); User FromUserItem(IReadOnlyDictionary<string,AttributeValue>); string UserKey(Guid id) /* "USER#{id:D}" */; string EmailKey(Email e) /* "EMAIL#{e.Value}" */ }`. Timestamps use the ISO-8601 round-trip format `"O"` in UTC.
  - `sealed class DynamoDbUserRepository(IAmazonDynamoDB db, IOptions<DynamoDbOptions> options) : IUserRepository`
    - `TryAddAsync` issues a `TransactWriteItems` of two `Put` operations, each with `attribute_not_exists(pk)`. It catches `TransactionCanceledException` where any `CancellationReasons[i].Code == "ConditionalCheckFailed"` and returns false.
    - Get operations use `ConsistentRead = true`.
  - Test helper: `sealed class DynamoDbLocalFixture : IAsyncLifetime { IAmazonDynamoDB Client; }` built on `Testcontainers.DynamoDb` with image `amazon/dynamodb-local:2.5.3`. Each test class creates a table with a unique name (`$"Users-{Guid.NewGuid():N}"`).

- [ ] **Step 1: Write failing tests**

```csharp
// Mapper (pure)
[Fact] ToUserItem_uses_spec_attribute_names() → keys == { "pk","_id","full_name","email","passwordHash","createdAt" }; pk == "USER#<id>"
[Fact] ToEmailGuardItem_keys_by_normalized_email() → pk == "EMAIL#ana@x.com"; userId == id
[Fact] FromUserItem_round_trips_ToUserItem()
// Repository (DynamoDB Local; [Collection("dynamodb")])
[Fact] TryAddAsync_then_GetByEmailAsync_returns_user()
[Fact] TryAddAsync_returns_false_for_duplicate_email_and_writes_no_second_user()  → second user has a different id and the same email; GetByIdAsync(second.Id) is null
[Fact] Concurrent_TryAddAsync_with_same_email_only_one_succeeds()                 → Task.WhenAll of 5 → exactly one true
[Fact] GetByIdAsync_returns_null_for_unknown_id()
[Fact] EnsureTableAsync_is_idempotent()                                          → call twice and expect no exception
```

- [ ] **Step 2: Run and confirm FAIL** with `dotnet test tests/TaskManager.Users.Infrastructure.Tests` (Docker must be running)
- [ ] **Step 3: Implement the building block, mapper and repository**
- [ ] **Step 4: Run and confirm PASS**
- [ ] **Step 5: Commit** with `feat(users): add DynamoDB user repository with email-guard uniqueness`

---

### Task 6: Users security adapters, metrics, seeding

**Files:**
- Create: `src/Users/TaskManager.Users.Infrastructure/Security/{AspNetPasswordHasher,JwtTokenIssuer}.cs`, `Seeding/{UserSeedOptions,UserSeeder}.cs`, `UsersDatabaseBootstrapper.cs`, `DependencyInjection.cs`
- Create: `src/BuildingBlocks/TaskManager.ApiCommon/EmfAppMetrics.cs`
- Create: `tests/TaskManager.Testing/TestJwt.cs`
- Test: `tests/TaskManager.Users.Infrastructure.Tests/{AspNetPasswordHasherTests,JwtTokenIssuerTests,UserSeederTests}.cs`, `tests/TaskManager.ApiCommon.Tests/EmfAppMetricsTests.cs`

**Interfaces:**
- Consumes: ports from Task 4, the repository from Task 5, `JwtOptions` from Task 2
- Produces:
  - `sealed class AspNetPasswordHasher : IPasswordHasher` wraps `PasswordHasher<object>`. `Verify` returns true for `Success` and `SuccessRehashNeeded`. A null hash verifies against a lazily computed dummy hash.
  - `sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer` uses `JsonWebTokenHandler` and `SecurityAlgorithms.HmacSha256`. Claims: `sub`=Id `D` format, `email`, `name`=FullName, plus `jti`. `ExpiresAt` = `clock.UtcNow + LifetimeMinutes`.
  - `sealed class UserSeedOptions { const string SectionName = "Seed"; bool Enabled; Guid DemoUserId = 11111111-1111-1111-1111-111111111111; string DemoEmail = "demo@taskmanager.dev"; string DemoPassword = "Demo123!"; string DemoFullName = "Demo User"; }`
  - `sealed class UserSeeder(IUserRepository repo, IPasswordHasher hasher, IClock clock, IOptions<UserSeedOptions> options) { Task SeedAsync(CancellationToken ct); }` is idempotent: it relies on `TryAddAsync` returning false.
  - `sealed class UsersDatabaseBootstrapper : IHostedService` runs `EnsureTableAsync` when `Dynamo:CreateTablesIfMissing`, then `SeedAsync` when `Seed:Enabled`.
  - `IServiceCollection AddUsersInfrastructure(this IServiceCollection, IConfiguration)` registers the repository, hasher, issuer, seeder, bootstrapper, `IClock` → `SystemClock`, the options and `AddDynamoDb`.
  - `sealed class EmfAppMetrics : IAppMetrics` writes one EMF document per call to stdout using namespace `TaskManager`, dimension `Service`=`IHostEnvironment.ApplicationName`, value 1, unit Count. It writes through an injectable `TextWriter` (default `Console.Out`) so it can be tested.
  - Test helper: `static string TestJwt.Create(Guid userId, string signingKey, DateTimeOffset? expires = null, string issuer = "taskmanager-users", string audience = "taskmanager-api")`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] Hash_then_Verify_succeeds(); [Fact] Verify_wrong_password_fails(); [Fact] Verify_null_hash_returns_false(); [Fact] Hash_does_not_contain_plain_password()
[Fact] Issue_produces_token_with_spec_claims_issuer_audience_and_60_minute_expiry()
    → validate with JsonWebTokenHandler + same key: iss "taskmanager-users", aud "taskmanager-api", sub == id, email, name; ExpiresAt == clock + 60m
[Fact] SeedAsync_creates_demo_user_with_fixed_id_and_verifiable_password()   (DynamoDB Local)
[Fact] SeedAsync_twice_is_idempotent()
[Fact] EmfAppMetrics_writes_valid_emf_json_with_metric_name()   → parse JSON: _aws.CloudWatchMetrics[0].Namespace == "TaskManager"; root["TaskCreated"] == 1
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run and confirm PASS**
- [ ] **Step 5: Commit** with `feat(users): add password hasher, JWT issuer, EMF metrics and demo seeding`

---

### Task 7: ApiCommon web plumbing

**Files:**
- Create: `src/BuildingBlocks/TaskManager.ApiCommon/{ApiControllerBase,GlobalExceptionHandler,CorrelationIdMiddleware,JwtBearerSetup,ApiDefaultsExtensions}.cs`
- Test: `tests/TaskManager.ApiCommon.Tests/{ApiControllerBaseTests,GlobalExceptionHandlerTests,CorrelationIdMiddlewareTests}.cs`

**Interfaces:**
- Consumes: `Error`, `ErrorType`, `JwtOptions`, `IAppMetrics`
- Produces:
  - `abstract class ApiControllerBase : ControllerBase`
    - `protected IActionResult Problem(Error error)` maps `Validation` to 400 `ValidationProblemDetails` (with `FieldErrors`), `Unauthorized` to 401, `NotFound` to 404 and `Conflict` to 409. In each case `detail` = `error.Message` and `extensions["code"]` = `error.Code`.
    - `protected Guid CurrentUserId` parses the `sub` claim and throws `InvalidOperationException` if it is missing.
  - `sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler>, IHostEnvironment) : IExceptionHandler` returns 500 ProblemDetails with title `An unexpected error occurred.` and `extensions["traceId"]`. It includes `exception` detail only in Development.
  - `sealed class CorrelationIdMiddleware` reads or creates `X-Correlation-Id`, echoes it on the response, and pushes it to the Serilog `LogContext` as `CorrelationId`.
  - `static WebApplicationBuilder AddTaskManagerApiDefaults(this WebApplicationBuilder builder)` sets up:
    - Serilog (compact JSON to console, `ReadFrom.Configuration`)
    - controllers with camelCase JSON plus `JsonStringEnumConverter`
    - `AddProblemDetails`, and an `InvalidModelStateResponseFactory` that returns 400 `ValidationProblemDetails`
    - `AddExceptionHandler<GlobalExceptionHandler>`
    - JWT bearer through `JwtBearerSetup.Configure(options, jwtOptions)`: `MapInboundClaims = false`, `ValidIssuer`, `ValidAudience`, `IssuerSigningKey`, `ClockSkew = 30s`. It also calls `jwtOptions.EnsureValid()` at startup.
    - CORS policy `"web"` from `Cors:AllowedOrigins` (string[]), with any header, any method
    - `AddOpenApi()`, health checks, `IAppMetrics` → `EmfAppMetrics`, and `AddAWSLambdaHosting(LambdaEventSource.HttpApi)`
  - `static WebApplication UseTaskManagerApiDefaults(this WebApplication app)` adds, in order:
    - `UseExceptionHandler`, then `CorrelationIdMiddleware`, then `UseSerilogRequestLogging`, then `UseCors("web")`, then `UseAuthentication`, then `UseAuthorization`
    - `MapControllers`
    - `MapHealthChecks("/health", predicate: _ => false)` and `MapHealthChecks("/health/ready", predicate: tag "ready")`
    - in Development only: `MapOpenApi` and `MapScalarApiReference`

- [ ] **Step 1: Write failing tests** (use a controller subclass on a `DefaultHttpContext`)

```csharp
[Theory, InlineData(ErrorType.Validation, 400), InlineData(ErrorType.Unauthorized, 401), InlineData(ErrorType.NotFound, 404), InlineData(ErrorType.Conflict, 409)]
Problem_maps_error_type_to_status(ErrorType t, int status)
[Fact] Problem_validation_includes_field_errors_and_code()
[Fact] CurrentUserId_reads_sub_claim()
[Fact] GlobalExceptionHandler_returns_500_without_exception_detail_outside_development()
[Fact] CorrelationId_is_echoed_when_provided_and_generated_when_absent()
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run and confirm PASS** with `dotnet test tests/TaskManager.ApiCommon.Tests`
- [ ] **Step 5: Commit** with `feat(api): add shared API defaults, ProblemDetails mapping and correlation id`

---

### Task 8: Users API (controllers, auth, rate limiting, health)

**Files:**
- Create in `src/Users/TaskManager.Users.Api/`: `Program.cs`, `Controllers/{AuthController,UsersController}.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (http 5080)
- Test: `tests/TaskManager.Users.Api.Tests/{UsersApiFactory,AuthEndpointsTests,UsersMeEndpointTests,HealthEndpointsTests}.cs`

**Interfaces:**
- Consumes: `IUserService` and DTOs (Task 4), `AddUsersInfrastructure` (Task 6), ApiCommon (Task 7)
- Produces:
  - `POST /api/v1/auth/register`: `201` + `UserResponse`, with `Location` `/api/v1/users/me`
  - `POST /api/v1/auth/login`: `200` + `AuthResponse`. It carries `[EnableRateLimiting("login")]`, a fixed window of `RateLimiting:LoginPermitLimit` (default 5) per 1 minute, partitioned by `RemoteIpAddress ?? "unknown"`, with `RejectionStatusCode = 429`.
  - `GET /api/v1/users/me` with `[Authorize]`: `200` + `UserResponse`, or 401
  - `public partial class Program;` for `WebApplicationFactory`
  - `appsettings.json` contains:
    - `Dynamo:TableName = "TaskManager-Users"`
    - `Cors:AllowedOrigins = ["http://localhost:4200"]`
    - `Seed:Enabled = false`, `Dynamo:CreateTablesIfMissing = false`
    - `Jwt` without `SigningKey`
  - `appsettings.Development.json` sets `Dynamo:ServiceUrl = "http://localhost:8000"`, `CreateTablesIfMissing = true` and `Seed:Enabled = true`. The `SigningKey` goes in user-secrets (`dotnet user-secrets set Jwt:SigningKey <value>`), never in a file.
  - The health check `DynamoTableHealthCheck` is registered with tag `ready`.
  - Test helper: `UsersApiFactory : WebApplicationFactory<Program>` sets environment `Testing` and the in-memory config `Jwt:SigningKey` = a 32+ char test key. It replaces `IUserService` with an NSubstitute substitute exposed as `Users`, removes `UsersDatabaseBootstrapper`, and replaces `IAmazonDynamoDB` with a substitute.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] Register_returns_201_with_location_and_body()
[Fact] Register_returns_409_problem_with_code_when_email_taken()          → extensions code "users.email_taken"
[Fact] Register_returns_400_validation_problem_with_field_errors()
[Fact] Register_with_malformed_json_returns_400_problem_not_500()        (Review Focus 4)
[Fact] Register_with_empty_body_returns_400()
[Fact] Login_returns_200_with_token()
[Fact] Login_returns_401_problem_with_message_Invalid_email_or_password()
[Fact] Login_sixth_request_within_a_minute_returns_429()
[Fact] Me_without_token_returns_401()
[Fact] Me_with_expired_token_returns_401()
[Fact] Me_with_wrong_signature_returns_401()
[Fact] Me_with_valid_token_calls_service_with_sub_and_returns_200()
[Fact] Health_returns_200_without_auth()
[Fact] Startup_fails_when_signing_key_missing()                           → factory with empty key; CreateClient throws InvalidOperationException
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement `Program.cs`** (`builder.AddTaskManagerApiDefaults(); builder.Services.AddUsersApplication().AddUsersInfrastructure(builder.Configuration); rate limiter; app.UseRateLimiter()` after routing) **and the thin controllers**, which bind the DTO, call the service, then `Ok`/`Created`/`Problem(error)`
- [ ] **Step 4: Run and confirm PASS** with `dotnet test tests/TaskManager.Users.Api.Tests`
- [ ] **Step 5: Smoke test against DynamoDB Local**

Run, in order:
- `docker compose up -d`
- `dotnet user-secrets set Jwt:SigningKey "local-dev-signing-key-change-me-0123456789" --project src/Users/TaskManager.Users.Api`
- `dotnet run --project src/Users/TaskManager.Users.Api`
- POST to `http://localhost:5080/api/v1/auth/login` with the demo credentials

Expected: `200` with `accessToken`, and `/health/ready` returns `200 Healthy`.

- [ ] **Step 6: Commit** with `feat(users): add Users API with register, login, me and health endpoints`

---

### Task 9: Tasks domain

**Files:**
- Create: `src/Tasks/TaskManager.Tasks.Domain/{TaskItemStatus,TaskItem}.cs`
- Test: `tests/TaskManager.Tasks.Domain.Tests/TaskItemTests.cs`

**Interfaces:**
- Produces:
  - `enum TaskItemStatus { ToDo, InProgress, Done }`
  - `sealed class TaskItem { Guid Id; Guid UserId; string Title; string? Description; TaskItemStatus Status; DateOnly? DueDate; DateTimeOffset CreatedAt; DateTimeOffset UpdatedAt; static Result<TaskItem> Create(Guid id, Guid userId, string? title, string? description, TaskItemStatus status, DateOnly? dueDate, DateTimeOffset now); static TaskItem Rehydrate(...all fields...); }`
  - **Field keys and messages:**
    - `title`: `Title is required.` and `Title must be at most 200 characters.`
    - `description`: `Description must be at most 2000 characters.`
    - `dueDate`: `Due date cannot be in the past.`
  - All errors are collected into one `Validation` error.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] Create_trims_title_sets_ToDo_and_equal_timestamps()
[Theory, InlineData(null), InlineData(""), InlineData("   ")] Create_requires_title
[Fact] Create_accepts_200_char_title_and_rejects_201()
[Fact] Create_converts_blank_description_to_null_and_rejects_2001_chars()
[Fact] Create_accepts_due_date_equal_to_today_utc()          → now 2026-11-10T23:59:59Z, due 2026-11-10 → success   (Review Focus 3)
[Fact] Create_rejects_due_date_before_today_utc()            → now 2026-11-10T00:00:00Z, due 2026-11-09 → dueDate error
[Fact] Create_reports_all_field_errors_together()
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run and confirm PASS**
- [ ] **Step 5: Commit** with `feat(tasks): add TaskItem entity with validation rules`

---

### Task 10: Tasks application layer

**Files:**
- Create in `src/Tasks/TaskManager.Tasks.Application/`:
  - `Contracts/{CreateTaskRequest,TaskResponse}.cs`
  - `Ports/ITaskRepository.cs`
  - `TaskErrors.cs`, `TasksMetrics.cs`, `ITaskService.cs`, `TaskService.cs`, `DependencyInjection.cs`
- Test: `tests/TaskManager.Tasks.Application.Tests/TaskServiceTests.cs`

**Interfaces:**
- Consumes: `TaskItem` (Task 9); `IClock`, `IAppMetrics`, `Result<T>`
- Produces:
  - **DTOs:**
    - `record CreateTaskRequest(string? Title, string? Description, string? Status, DateOnly? DueDate)`. `Status` is parsed case-insensitively with `Enum.TryParse` and must also satisfy `Enum.IsDefined`. Null means `ToDo`; an unknown value is a validation error on `status` with the message `Status must be one of: ToDo, InProgress, Done.`
    - `record TaskResponse(Guid Id, string Title, string? Description, string Status, DateOnly? DueDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)`
  - **Repository:** `interface ITaskRepository { Task AddAsync(TaskItem task, CancellationToken ct); Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, CancellationToken ct); Task<TaskItem?> GetAsync(Guid userId, Guid taskId, CancellationToken ct); }`
  - **`TaskErrors.NotFound`** = `Error.NotFound("tasks.not_found", "Task not found.")`; **`TasksMetrics.TaskCreated`**
  - **Service:** `interface ITaskService { Task<Result<TaskResponse>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct); Task<Result<IReadOnlyList<TaskResponse>>> ListForUserAsync(Guid userId, CancellationToken ct); Task<Result<TaskResponse>> GetByIdAsync(Guid userId, Guid taskId, CancellationToken ct); }`. The list is sorted by `CreatedAt` descending.
  - **DI:** `AddTasksApplication(this IServiceCollection)`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] CreateAsync_persists_task_owned_by_caller_and_returns_response()   → repo.Received().AddAsync(Arg.Is<TaskItem>(t => t.UserId == userId && t.Status == ToDo))
[Fact] CreateAsync_parses_status_case_insensitively()                      → "inprogress" → "InProgress"
[Fact] CreateAsync_rejects_unknown_status_with_field_error()               (Review Focus 4)
[Fact] CreateAsync_returns_validation_error_and_does_not_persist_for_blank_title()
[Fact] CreateAsync_increments_TaskCreated()
[Fact] ListForUserAsync_returns_newest_first()
[Fact] GetByIdAsync_returns_NotFound_when_task_belongs_to_another_user()   → repo.GetAsync(callerId, taskId) returns null   (Review Focus 2)
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run and confirm PASS**
- [ ] **Step 5: Commit** with `feat(tasks): add task service for create, list and get`

---

### Task 11: Tasks infrastructure (persistence, seeding)

**Files:**
- Create in `src/Tasks/TaskManager.Tasks.Infrastructure/`:
  - `Persistence/{TasksTableDefinition,TaskItemMapper,DynamoDbTaskRepository}.cs`
  - `Seeding/{TaskSeedOptions,TaskSeeder}.cs`, `TasksDatabaseBootstrapper.cs`, `DependencyInjection.cs`
- Test: `tests/TaskManager.Tasks.Infrastructure.Tests/{TaskItemMapperTests,DynamoDbTaskRepositoryTests,TaskSeederTests}.cs`

**Interfaces:**
- Consumes: `ITaskRepository` (Task 10), the DynamoDb building block (Task 5), `DynamoDbLocalFixture`
- Produces:
  - `TasksTableDefinition.Create(string tableName)`: HASH `userId` S, RANGE `_id` S, `PAY_PER_REQUEST`
  - `static class TaskItemMapper { Dictionary<string,AttributeValue> ToItem(TaskItem); TaskItem FromItem(IReadOnlyDictionary<string,AttributeValue>); }`
    - `description` and `due_date` attributes are **omitted** when null.
    - `due_date` uses format `yyyy-MM-dd`.
    - `status` is the enum name.
  - `sealed class DynamoDbTaskRepository(IAmazonDynamoDB, IOptions<DynamoDbOptions>) : ITaskRepository`
    - `AddAsync` uses `PutItem` with `attribute_not_exists(#id)`.
    - `ListByUserAsync` uses `Query` with `userId = :u`, loops on `LastEvaluatedKey`, and sets `ConsistentRead = true`.
    - `GetAsync` uses `GetItem(userId, _id)`.
  - `sealed class TaskSeedOptions { const string SectionName = "Seed"; bool Enabled; Guid DemoUserId = 11111111-1111-1111-1111-111111111111; }`
  - `sealed class TaskSeeder(ITaskRepository, IClock, IOptions<TaskSeedOptions>) { Task SeedAsync(CancellationToken ct); }`. It skips seeding if the demo user already has any task; otherwise it adds 4 tasks:
    - "Explore the Task Manager", `InProgress`, due today + 3
    - "Review sprint backlog", `ToDo`, due today + 7
    - "Set up local environment", `Done`, no due date
    - "Write project notes", `ToDo`, no due date
  - `TasksDatabaseBootstrapper : IHostedService` and `AddTasksInfrastructure(this IServiceCollection, IConfiguration)`, mirroring the Users versions (`IClock` → `SystemClock`)

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] ToItem_uses_spec_attribute_names_and_omits_null_optional_fields()
[Fact] FromItem_round_trips_including_due_date_and_status()
[Fact] ListByUserAsync_returns_only_that_users_tasks()                   (two users seeded)
[Fact] GetAsync_with_other_users_id_returns_null()                       (Review Focus 2)
[Fact] ListByUserAsync_pages_through_more_than_one_page()                → insert 30 tasks each with a 40 KB description (≈1.2 MB > 1 MB page) → count 30
[Fact] SeedAsync_adds_four_tasks_once()                                  → run twice → 4
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run and confirm PASS**
- [ ] **Step 5: Commit** with `feat(tasks): add DynamoDB task repository and demo task seeding`

---

### Task 12: Tasks API

**Files:**
- Create in `src/Tasks/TaskManager.Tasks.Api/`: `Program.cs`, `Controllers/TasksController.cs`, `appsettings.json` (`Dynamo:TableName = "TaskManager-Tasks"`), `appsettings.Development.json`, `Properties/launchSettings.json` (http 5090)
- Test: `tests/TaskManager.Tasks.Api.Tests/{TasksApiFactory,TasksEndpointsTests,HealthEndpointsTests}.cs`

**Interfaces:**
- Consumes: `ITaskService` and DTOs (Task 10), `AddTasksInfrastructure` (Task 11), ApiCommon (Task 7), `TestJwt` (Task 6)
- Produces: `[Authorize] [Route("api/v1/tasks")] TasksController : ApiControllerBase` with:
  - `POST` returning `201 CreatedAtAction(GetById)`
  - `GET` returning `200` and a list
  - `GET {id:guid}` returning `200` or `404`
  
  Every action passes `CurrentUserId` to the service. The Tasks API uses the same `Jwt` section, and its user-secrets value must equal the Users API key.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] Create_without_token_returns_401()
[Fact] Create_with_token_returns_201_location_and_uses_sub_as_owner()    → Tasks.Received().CreateAsync(userIdFromToken, ...)
[Fact] Create_ignores_userId_in_body()                                   → body {"title":"x","userId":"<other>"} → service called with the token's sub
[Fact] Create_with_unknown_status_returns_400_validation_problem()      (Review Focus 4)
[Fact] Create_with_malformed_json_returns_400_problem()                 (Review Focus 4)
[Fact] Create_with_invalid_due_date_format_returns_400()                 → "dueDate":"10/11/2026"
[Fact] List_returns_200_with_tasks_for_caller()
[Fact] GetById_for_other_users_task_returns_404()                        (Review Focus 2)
[Fact] Token_signed_with_different_key_returns_401()
[Fact] Health_returns_200_without_auth()
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement `Program.cs` and the controller**
- [ ] **Step 4: Run and confirm PASS**, then run the full suite with `dotnet test TaskManager.slnx`
- [ ] **Step 5: Smoke test**

Run the Tasks API with the same signing key in user-secrets. Use the demo token from Task 8, call `GET http://localhost:5090/api/v1/tasks`, then `POST` a task.

Expected: the 4 seeded tasks come back, and the POST returns `201` with the new task.

- [ ] **Step 6: Commit** with `feat(tasks): add Tasks API with create, list and get endpoints`

---

### Task 13: Angular scaffold, runtime config, core auth

**Files:**
- Create the workspace with `npx @angular/cli@latest new web --style=scss --routing --ssr=false --skip-git`, then `ng add @angular/material` and `ng add angular-eslint`.
- Create:
  - `web/public/config.json`: `{ "usersApiUrl": "http://localhost:5080", "tasksApiUrl": "http://localhost:5090" }`
  - `web/src/app/core/config/app-config.ts`
  - `web/src/app/core/http/{app-error.ts,error-interceptor.ts}`
  - `web/src/app/core/auth/{auth.models.ts,auth-api.ts,auth-store.ts,auth-interceptor.ts,auth-guard.ts,guest-guard.ts}`
- Modify: `web/src/main.ts` and `web/src/app/app.config.ts`. Set strict TS and strict templates in `tsconfig.json`; they are on by default, so verify them.
- Test: a `*.spec.ts` next to each core file

**Interfaces:**
- Consumes: the Users API contract (Task 8)
- Produces:
  - `interface AppConfig { usersApiUrl: string; tasksApiUrl: string }`, `const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG')`, and `function loadAppConfig(): Promise<AppConfig>` (which fetches `/config.json`). `main.ts` awaits it and passes `{ provide: APP_CONFIG, useValue }` to `bootstrapApplication`.
  - `interface AppError { status: number; message: string; code?: string; fieldErrors: Record<string, string[]> }` and `function toAppError(e: HttpErrorResponse): AppError`. It reads ProblemDetails `detail`, `errors` and `code`; status 0 maps to `Unable to reach the server. Please try again.`
  - Models: `User { id; fullName; email; createdAt }`, `AuthResponse { accessToken; expiresAt; user }`, `LoginRequest`, `RegisterRequest`
  - `AuthApi` (`providedIn: 'root'`) has `login(req): Observable<AuthResponse>`, `register(req): Observable<User>` and `me(): Observable<User>`.
  - `AuthStore` (`providedIn: 'root'`):
    - signals `user: Signal<User|null>` and `token: Signal<string|null>`, plus computed `isAuthenticated`
    - `login(req): Promise<void>` (rejects with `AppError`), `register(req): Promise<void>` (registers then logs in), `logout(): void`, `restoreSession(): Promise<void>`
    - It persists `tm.session` = `{ token, expiresAt, user }` in `sessionStorage`.
    - `restoreSession` drops the session when it has expired; otherwise it calls `me()` and clears the session on failure.
    - It is wired with `provideAppInitializer(() => inject(AuthStore).restoreSession())`.
  - `authInterceptor: HttpInterceptorFn` adds `Authorization: Bearer` **only** when the URL starts with `usersApiUrl` or `tasksApiUrl` and a token exists.
  - `errorInterceptor: HttpInterceptorFn` handles a 401 whose URL does **not** contain `/api/v1/auth/` by calling `AuthStore.logout()` and navigating to `/login` with `{ returnUrl: router.url, reason: 'expired' }`. It always rethrows `toAppError(err)`.
  - `authGuard` returns `true` or a `UrlTree` to `/login?returnUrl=…`; `guestGuard` returns `true` or a `UrlTree` to `/tasks`.
  - `app.config.ts`: `provideZonelessChangeDetection()` (if it isn't already the default), `provideRouter(routes, withComponentInputBinding())`, `provideHttpClient(withInterceptors([authInterceptor, errorInterceptor]))`, `provideAnimationsAsync()`

- [ ] **Step 1: Write failing specs** (Vitest + `TestBed` + `provideHttpClientTesting`)

```ts
it('authInterceptor attaches bearer only to configured API origins')
it('errorInterceptor redirects to /login with reason=expired on 401 from tasks API')
it('errorInterceptor does NOT redirect on 401 from /api/v1/auth/login')        // Review Focus 5
it('toAppError maps ProblemDetails detail, code and errors')
it('toAppError maps status 0 to the "Unable to reach the server" message')
it('AuthStore.login stores session in sessionStorage and sets user')
it('AuthStore.restoreSession clears an expired session without calling /me')
it('AuthStore.restoreSession clears session when /me returns 401')
it('authGuard redirects unauthenticated users with returnUrl')
it('guestGuard redirects authenticated users to /tasks')
```

- [ ] **Step 2: Run and confirm FAIL** with `cd web && npx ng test --watch=false`
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run and confirm PASS**, plus `npx ng lint` reporting 0 problems
- [ ] **Step 5: Commit** with `feat(web): scaffold Angular app with runtime config and auth core`

---

### Task 14: Angular login, register, shell

**Files:**
- Create:
  - `web/src/app/layout/shell/{shell.ts,shell.html,shell.scss}`
  - `web/src/app/features/auth/login/{login.ts,login.html,login.scss}`
  - `web/src/app/features/auth/register/{register.ts,register.html,register.scss}`
  - `web/src/app/shared/ui/form-error/form-error.ts`
- Modify: `web/src/app/app.routes.ts`
- Test: `login.spec.ts`, `register.spec.ts`, `shell.spec.ts`

**Interfaces:**
- Consumes: `AuthStore`, `AppError`, guards (Task 13)
- Produces:
  - **Routes:**
    - `''` → redirect `/tasks`
    - `login` (guestGuard, lazy `Login`) and `register` (guestGuard, lazy `Register`)
    - `''` with `Shell` + `authGuard`, child `tasks` (lazy route from Task 15)
    - `**` → `/tasks`
  - `FormError` component: `input.required<string[] | undefined>()`, which renders `<mat-error>` items.
  - **Copy:**
    - Login title: `Sign in`. Demo hint: `Demo account: demo@taskmanager.dev / Demo123!`. Button: `Use demo account`.
    - Invalid credentials alert (`role="alert"`): `Invalid email or password.`
    - Session-expired notice (`reason=expired`): `Your session has expired. Please sign in again.`
    - Register title: `Create account`. Confirm-password mismatch: `Passwords do not match.`

- [ ] **Step 1: Write failing specs**

```ts
it('Login: "Use demo account" fills email and password')
it('Login: on 401 shows "Invalid email or password.", keeps email, clears password, does not navigate')   // Story 1 AC
it('Login: on success navigates to returnUrl or /tasks')
it('Login: submit disabled while request in flight; required/email validators show messages')
it('Login: shows session-expired notice when reason=expired')
it('Register: password mismatch shows "Passwords do not match." and does not submit')
it('Register: 409 sets server error on the email control')
it('Register: success navigates to /tasks')
it('Shell: shows user full name and "Sign out" logs out and navigates to /login')
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement**

Use standalone components with `ChangeDetectionStrategy.OnPush`, typed `FormBuilder.nonNullable`, and Material form fields. Use a responsive layout: a centered card capped at `max-width: 420px` on small screens and a top toolbar.

- [ ] **Step 4: Run and confirm PASS**, plus lint with 0 problems
- [ ] **Step 5: Commit** with `feat(web): add login, register and app shell`

---

### Task 15: Angular tasks feature (add + list with confirmation)

**Files:**
- Create in `web/src/app/features/tasks/`:
  - `data/{task.model.ts,tasks-api.ts,tasks-store.ts}`
  - `task-form/{task-form.ts,task-form.html,task-form.scss}`, `task-list/{task-list.ts,task-list.html,task-list.scss}`
  - `tasks-page/{tasks-page.ts,tasks-page.html,tasks-page.scss}`, `tasks.routes.ts`
- Create: `web/src/app/shared/ui/{empty-state/empty-state.ts,status-badge/status-badge.ts}`
- Test: a spec for the store, the API, and each component

**Interfaces:**
- Consumes: `APP_CONFIG`, `AppError`, `FormError`
- Produces:
  - `type TaskStatus = 'ToDo' | 'InProgress' | 'Done'`; `interface Task { id; title; description: string | null; status: TaskStatus; dueDate: string | null; createdAt; updatedAt }`; `interface CreateTaskRequest { title; description?: string | null; status?: TaskStatus; dueDate?: string | null }`
  - `TasksApi`: `list(): Observable<Task[]>`, `create(req): Observable<Task>`
  - `TasksStore` (provided in `TasksPage` providers): signals `tasks`, `loading`, `error: Signal<AppError|null>`; `load(): Promise<void>`; `create(req): Promise<Task>` prepends the new task to `tasks`.
  - `TaskForm`: `submitting = input(false)`, `serverErrors = input<Record<string,string[]>>({})`, `submitted = output<CreateTaskRequest>()`. It resets after a successful create via `reset()` called by the parent through `viewChild`.
  - `TaskList`: `tasks = input.required<Task[]>()`, rendered with `@for (task of tasks(); track task.id)` and `@empty` → `EmptyState` with the message `No tasks yet — add your first one`.
  - `StatusBadge`: `status = input.required<TaskStatus>()`, with labels `To do`, `In progress` and `Done`.
  - `TasksPage` calls `store.load()` on init. On create success it shows a `MatSnackBar` `Task created` (3 s) and resets the form; on failure it passes `fieldErrors` to the form.
  - **Layout:** a two-column grid at ≥ 960 px (form | list) and a single column below that.
  - The due-date input uses `type="date"` with `min` set to today's local date. The server remains authoritative.

- [ ] **Step 1: Write failing specs**

```ts
it('TasksStore.create prepends task to list')
it('TasksStore.load sets loading and stores tasks; sets error on failure')
it('TaskForm: title required, max 200; description max 2000; emits trimmed request with default status ToDo')
it('TaskForm: displays server field errors under matching controls')
it('TaskList: renders empty state when no tasks')
it('TaskList: renders title, status badge and due date for each task')
it('TasksPage: after create shows "Task created" snackbar and new task first')   // Story 1 AC
```

- [ ] **Step 2: Run and confirm FAIL**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run and confirm PASS**, `npx ng lint` with 0 problems, and `npx ng build` succeeding with no warnings
- [ ] **Step 5: Manual browser verification**

Run, in order:
- `docker compose up -d`
- both APIs
- `cd web && npx ng serve`

In the browser pane at `http://localhost:4200`, check every step:
1. A wrong password shows the alert and nothing else happens.
2. "Use demo account" signs in and the 4 seeded tasks appear.
3. Adding a task with a title only shows the snackbar, and the task appears first.
4. Register a new user and add a task; that user does **not** see the demo tasks.
5. Refreshing the page keeps the session.
6. Sign out works.
7. The layout holds at 375 px wide.

Expected: `read_console_messages` returns **0 warnings and 0 errors** on every screen.

- [ ] **Step 6: Commit** with `feat(web): add tasks page with add-task form and task list`

---

### Task 16: CI, PR review workflow, coverage gate, symlink guard

**Files:**
- Create: `.github/workflows/ci.yml`, `.github/workflows/pr-review.yml`, `coverlet.runsettings`
- Modify: `Directory.Build.props` (coverage settings for test projects)

**Interfaces:**
- Produces: required status-check names `backend`, `frontend` and `repo-hygiene`, which branch protection uses later.

- [ ] **Step 1: Write `ci.yml`**

It triggers on `pull_request` and on `push` to `main`, with `permissions: contents: read` and `concurrency` per ref. It has three jobs:
- **`backend`** (`ubuntu-latest`):
  - `actions/setup-dotnet@v4` with `global-json-file: global.json`
  - `dotnet restore`, then `dotnet build -warnaserror --no-restore`, then `dotnet test --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings`
  - `dotnet tool run reportgenerator` (declared in `.config/dotnet-tools.json`) merges the reports. The job fails if line coverage of `TaskManager.*.Domain` + `TaskManager.*.Application` is below 80.
  - The coverage report is uploaded as an artifact.
- **`frontend`**: `actions/setup-node@v4` (`node-version: lts/*`, npm cache `web/package-lock.json`), then `npm ci`, `npx ng lint --max-warnings 0`, `npx ng test --watch=false` and `npx ng build`, all with `working-directory: web`.
- **`repo-hygiene`**: `test -z "$(git ls-files -s | awk '$1 == "120000"')"` with the message `Symbolic links are not allowed in this repository.`

- [ ] **Step 2: Verify the symlink guard locally**

Run the hygiene command from Step 1 in Git Bash.
Expected: exit code 0 and empty output.

- [ ] **Step 3: Write `pr-review.yml`**

- Trigger: `on: pull_request: types [opened, synchronize, ready_for_review]`
- Permissions: `contents: read`, `pull-requests: write`, `issues: write`
- Step: `anthropics/claude-code-action@v1` with `anthropic_api_key: ${{ secrets.ANTHROPIC_API_KEY }}`
- Prompt: review the PR diff against `.claude/rules/rules.md` and the spec, with fresh eyes, focused on correctness, security, test coverage and the architecture rules. Post inline comments and one summary comment. Never approve, request changes, resolve threads, push commits or merge.

- [ ] **Step 4: Validate the workflow YAML**

Run: `npx --yes @action-validator/cli .github/workflows/ci.yml` (and the same for `pr-review.yml`)
Expected: no errors.

- [ ] **Step 5: Commit** with `ci: add build/test/coverage workflow, symlink guard and AI PR review`

---

### Task 17: Documentation (README, rules.md, GENAI.md) and final verification

**Files:**
- Create: `README.md`, `.claude/rules/rules.md`, `GENAI.md`

**Interfaces:**
- Consumes: the facts from every earlier task (ports, commands, demo credentials, workflow names)

- [ ] **Step 1: Write `README.md`**

It has these sections:
- Overview
- Architecture: a Mermaid diagram of Angular → Users API / Tasks API → DynamoDB, plus the layer diagram
- Prerequisites: .NET SDK 10.0.401+, Node LTS, Docker
- Quick start: the exact commands from Task 8 Step 5 and Task 15 Step 5, including user-secrets with the **same** key for both APIs
- Demo credentials
- Running tests: backend, frontend and coverage
- Configuration reference: the `Dynamo`, `Jwt`, `Seed`, `Cors` and `RateLimiting` sections, and `config.json`
- API overview, with the Scalar URLs `/scalar/v1`
- Project layout
- CI and PR review, including the setup steps for the `ANTHROPIC_API_KEY` secret and branch protection
- Known limitations: no email verification, no refresh tokens, `sessionStorage` token, no E2E suite, deployment pending in sub-project 3

- [ ] **Step 2: Write `.claude/rules/rules.md`**

It contains these rules:
- Clean Architecture and the dependency rule, plus Controller → Service → Repository with DTOs. No EF, Dapper, MediatR or handler-per-use-case.
- TDD (red/green/refactor) and test naming `Method_condition_expected`.
- SOLID, DRY, KISS and YAGNI, each with a one-line project-specific example.
- .NET conventions: nullable enabled, warnings as errors, `Result<T>` instead of exceptions for business failures, never log secrets.
- Angular conventions per the angular.dev style guide: standalone, signals, OnPush, `inject()`, no `.component` suffix, feature folders, zero console warnings.
- Conventional Commits and a branch per story.
- **No symbolic links.**
- PR rules: CI green; the AI reviewer only comments; a human resolves threads and merges.

- [ ] **Step 3: Write `GENAI.md`**

It covers:
- **Tool:** Claude Code (Claude Opus 5.5) with the Superpowers plugin (brainstorming → spec → plan → TDD execution → code review)
- **The original prompt**, verbatim, copied from the session's first message
- **Validation:** TDD, architecture tests, CI, the coverage gate, manual browser checks and human design review
- **Corrections:**
  - handler-per-use-case replaced by Controller → Service → Repository at the human's request
  - unique email via a guard item instead of a GSI
  - the justification for `/users/me`
  - email verification scoped out (SES sandbox)
  - the no-symlinks rule with its CI enforcement
  - any deviations found during execution
- **Edge cases, auth and validation:** a reference to the Review Focus items and how each is tested

- [ ] **Step 4: Full verification**

Run, in order:
- `dotnet build TaskManager.slnx -warnaserror`
- `dotnet test TaskManager.slnx`
- `cd web && npx ng lint && npx ng test --watch=false && npx ng build`

Expected: everything passes, with 0 warnings.

- [ ] **Step 5: Commit** with `docs: add README, collaborator rules and GENAI notes`

- [ ] **Step 6: Hand off**

Use `superpowers:finishing-a-development-branch` to push the branch and open the PR "closes #15". Ask the human before pushing, creating the PR, or applying branch protection.
