# Design Spec — Sub-project 1: Foundation + Story 1 (Sign in and add my first task)

- **Date:** 2026-11-10
- **Status:** Draft — awaiting human review
- **Issue:** yamilkar/TaskManager#15 — *Story 1: Sign in and add my first task*
- **Purpose of the project:** technical assessment / portfolio piece. Clean code, tests, and documentation come first; AWS must stay low-cost (free tier) and easy to tear down.

---

## 1. Context and scope

### 1.1 Overall product

A task-management application where users register, sign in, and create, update, delete, and list **their own** tasks only. Components: a **Users API**, a **Tasks API** (both ASP.NET Core), shared AWS storage with one table for users and one for tasks, and an **Angular** client.

### 1.2 Decomposition

The overall request spans several subsystems and is split into three sub-projects. Each one gets its own spec, plan, and PR.

| # | Sub-project | Contents |
|---|---|---|
| **1** | **Foundation + Story 1** *(this spec)* | Solution structure, DynamoDB storage, Users API (register, login, `/users/me`), Tasks API (create, list, get by id), Angular sign-in / register / add-task / list screens, seeded demo data, health endpoints, logging and metrics, unit tests, CI with tests gating merges, PR review workflow, README / `rules.md` / `GENAI.md` |
| 2 | Remaining task CRUD | Update and delete in the API and UI; list refinements |
| 3 | AWS deployment + CD | Infrastructure as code, Lambda / API Gateway / S3 / CloudFront, continuous-deploy workflow |

This spec still **fixes the AWS storage and hosting choice** (section 2), because it shapes the data layer in sub-project 1.

### 1.3 Story 1 acceptance criteria (from #15)

- A demo account is available to sign in with.
- Wrong credentials show a clear message and nothing else happens.
- After signing in I can add a task by giving it a title, and I see it confirmed.

### 1.4 Hard constraints (from the original request)

- Clean Architecture, separation of concerns, independent components; TDD; SOLID, DRY, KISS, YAGNI.
- **No Entity Framework, no Dapper, no MediatR**, and no mediator-style pattern (section 9, D1).
- One table/container for users and one for tasks.
  - Task fields: `_id` (Guid), `userId`, `title`, `description`, `status`, `due_date`, `createdAt`, `updatedAt`.
  - User fields: `_id` (Guid), `full_name`, `email`, `passwordHash`, `createdAt`.
- Seeded data and demo credentials.
- Modern AWS storage, plus AWS logging and metrics.
- Both APIs expose health endpoints; the Users API has both authorized and non-authorized endpoints.
- Unit tests for the data access layer, business logic layer, and API endpoints.
- Angular app that is responsive, follows https://angular.dev/style-guide, and has **no browser console warnings or errors**.
- README, `rules.md` for collaborators, and `GENAI.md` at the repo root.
- GitHub CI/CD; all tests must pass before merging. AI (Superpowers) reviews PRs with comments but can never resolve or merge; a human merges.
- **No symbolic links in the repository** (security requirement).

---

## 2. Chosen approach: serverless on AWS (approach A)

| Concern | Choice |
|---|---|
| Storage | **Amazon DynamoDB**: tables `TaskManager-Users` and `TaskManager-Tasks`, accessed with the low-level AWS SDK (`AmazonDynamoDBClient`) and hand-written mappers |
| API hosting (sub-project 3) | Each ASP.NET Core API runs on **AWS Lambda behind API Gateway (HTTP API)** via `Amazon.Lambda.AspNetCoreServer.Hosting`; locally it runs as a normal Kestrel web app |
| UI hosting (sub-project 3) | **S3 + CloudFront** |
| Logging | Structured JSON logs (Serilog) to stdout, which Lambda forwards to **CloudWatch Logs** |
| Metrics | **CloudWatch Embedded Metric Format (EMF)** custom metrics |
| Secrets | JWT signing key in **AWS Secrets Manager** (AWS), `dotnet user-secrets` (local) |
| Local dev | **DynamoDB Local** via `docker compose` |

**Rejected alternatives:**
- **B. DynamoDB + App Runner/ECS:** about $10–30/month idle.
- **C. Aurora Serverless v2 with raw Npgsql:** about $40+/month, VPC networking, hand-written SQL mapping.

Approach A costs about $0 idle, stays in the free tier, and keeps hosting concerns out of the Domain and Application layers.

---

## 3. Solution structure and layering

### 3.1 Layout

```
TaskManager/
├─ src/
│  ├─ BuildingBlocks/TaskManager.SharedKernel      Result<T>, Error, ErrorType, IClock (no AWS/ASP.NET refs)
│  ├─ Users/
│  │  ├─ TaskManager.Users.Domain          User entity, Email value object, invariants
│  │  ├─ TaskManager.Users.Application     IUserService/UserService (RegisterAsync, LoginAsync, GetByIdAsync)
│  │  │                                    DTOs: RegisterUserRequest, LoginRequest, UserResponse, AuthResponse
│  │  │                                    Ports: IUserRepository, IPasswordHasher, ITokenIssuer
│  │  │                                    Hand-written request validators
│  │  ├─ TaskManager.Users.Infrastructure  DynamoDbUserRepository, UserItemMapper, PBKDF2 hasher adapter,
│  │  │                                    JwtTokenIssuer, table initializer, seeder, EMF metrics
│  │  └─ TaskManager.Users.Api             AuthController, UsersController, health, ProblemDetails,
│  │                                       JWT auth, rate limiting, CORS, OpenAPI, Lambda hosting
│  └─ Tasks/
│     ├─ TaskManager.Tasks.Domain          TaskItem entity, TaskItemStatus, invariants
│     ├─ TaskManager.Tasks.Application     ITaskService/TaskService (CreateAsync, ListForUserAsync, GetByIdAsync)
│     │                                    DTOs: CreateTaskRequest, TaskResponse; Port: ITaskRepository
│     ├─ TaskManager.Tasks.Infrastructure  DynamoDbTaskRepository, TaskItemMapper, table initializer, seeder, metrics
│     └─ TaskManager.Tasks.Api             TasksController, health, ProblemDetails, JWT validation, CORS, OpenAPI
├─ tests/                                  one xUnit project per src project + TaskManager.ArchitectureTests
├─ web/                                    Angular app
├─ docker-compose.yml                      DynamoDB Local (+ dynamodb-admin UI)
├─ .github/workflows/                      ci.yml, pr-review.yml
├─ .claude/rules/rules.md                  collaborator rules (a real file, not a symlink)
├─ docs/superpowers/specs|plans/
├─ README.md
└─ GENAI.md
```

### 3.2 Design pattern: Controller → Service → Repository

- **Controllers** are thin. They bind the request DTO, take `userId` from the JWT `sub` claim, call the service, and map `Result<T>` to an HTTP response.
- **Services** (Application layer) hold the business rules and validation, orchestrate domain entities and ports, and take and return **DTOs**. Domain entities never leave the Application layer.
- **Repositories** are interfaces defined in Application and implemented in Infrastructure.
- No MediatR and no handler-per-use-case classes. If a service grows too many responsibilities later, splitting it into use-case handlers can be reconsidered (section 9, D1).

### 3.3 Dependency rule

`Api → Application → Domain`; `Infrastructure → Application`. Only the Api project (the composition root) references Infrastructure. Domain and Application reference no AWS or ASP.NET packages. **NetArchTest** tests enforce this in CI.

### 3.4 Component independence

Users and Tasks share nothing except `SharedKernel`. The Tasks API never reads the Users table; it trusts the `sub` claim of a JWT signed by the Users API using a shared signing key.

### 3.5 Stack

- **Backend:** .NET 10 (LTS); xUnit, NSubstitute, Shouldly; Testcontainers; NetArchTest; coverlet.
- **Password hashing:** `PasswordHasher<T>` (PBKDF2) from `Microsoft.Extensions.Identity.Core`.
- **Frontend:** latest stable Angular with Angular Material (M3) and Vitest.

---

## 4. Data model and DynamoDB design

### 4.1 Users table: `TaskManager-Users`

Partition key `pk` (String), no sort key. It holds two item kinds:

| Item | `pk` | Attributes |
|---|---|---|
| User | `USER#<guid>` | `_id`, `full_name`, `email`, `passwordHash`, `createdAt` |
| Email guard | `EMAIL#<normalized email>` | `userId` |

- **Why the guard item:** DynamoDB enforces uniqueness only on primary keys, and a GSI cannot reserve a value. It is eventually consistent and allows a check-then-write race.
- **Register:** one `TransactWriteItems` writes the user item and the guard item, both with `attribute_not_exists(pk)`. If the email is taken, DynamoDB cancels the whole transaction (`TransactionCanceledException` / `ConditionalCheckFailed`). The service maps that to `Error.Conflict("EmailAlreadyRegistered")`, which becomes HTTP **409**. This holds under concurrent requests, and it never leaves an orphan user or guard.
- **Login:** a strongly consistent `GetItem(EMAIL#…)` returns `userId`, then `GetItem(USER#…)` returns the user. No GSI is needed.
- **Email normalization:** trim, then lower-invariant, applied before storing or looking up.

### 4.2 Tasks table: `TaskManager-Tasks`

Partition key `userId` (String), sort key `_id` (String). Attributes: `_id`, `userId`, `title`, `description`, `status`, `due_date`, `createdAt`, `updatedAt`.

- Every read and write is addressed by `(userId from JWT, taskId)`, so ownership is enforced by the key itself.
- Listing is one `Query` on `userId`, paginated internally until `LastEvaluatedKey` is empty. It needs no scan and no GSI.

### 4.3 Types and business rules

| Field | Rule |
|---|---|
| `_id` | Guid, generated server-side |
| `title` | required, trimmed, 1–200 chars |
| `description` | optional, ≤ 2000 chars (trimmed; empty → null) |
| `status` | `ToDo` \| `InProgress` \| `Done`; default `ToDo`; stored as a string |
| `due_date` | optional, date-only `yyyy-MM-dd`; must not be before today in the user's time zone at creation; server rule is `dueDate >= utcToday - 1 day` using the `IClock` UTC date (see D9); the client enforces local today |
| `createdAt` / `updatedAt` | ISO-8601 UTC strings from `IClock`; equal at creation |
| `full_name` | required, trimmed, 1–100 chars |
| `email` | required, valid format, ≤ 254 chars, normalized |
| password | ≥ 8 chars, at least one letter and one digit, ≤ 128 chars; hashed with PBKDF2; never stored or logged in plain text |

Invariants live in the domain entities (factory methods that return `Result<T>`). Request-shape validation lives in Application validators. Both are unit-tested.

### 4.4 Table creation and seeding

- Each API creates **its own** table if it is missing, but only when `Dynamo:CreateTablesIfMissing=true` (local and DynamoDB Local). In AWS, tables come from IaC (sub-project 3).
- Seeding runs when `Seed:Enabled=true` and is **idempotent** (conditional puts; existing items are skipped).
  - The Users API seeds the demo user with a fixed Guid from config (`Seed:DemoUserId`).
  - The Tasks API seeds 3–4 sample tasks for that same Guid.
- Demo credentials: **`demo@taskmanager.dev` / `Demo123!`**. They are documented in the README and shown as a hint on the login page.

---

## 5. APIs

### 5.1 Users API

| Method | Route | Auth | Responses |
|---|---|---|---|
| POST | `/api/v1/auth/register` | anonymous | `201` `UserResponse` + `Location: /api/v1/users/me`; `400` validation; `409` email taken |
| POST | `/api/v1/auth/login` | anonymous | `200` `AuthResponse { accessToken, expiresAt, user }`; `400`; `401`; `429` |
| GET | `/api/v1/users/me` | **JWT** | `200` `UserResponse`; `401` |
| GET | `/health` | anonymous | liveness |
| GET | `/health/ready` | anonymous | readiness (DynamoDB `DescribeTable` on the Users table) |

`/users/me` is the Users API's authorized endpoint, which the requirements call for. The Angular app also uses it to validate a stored session on startup.

### 5.2 Tasks API (all routes need a JWT except health)

| Method | Route | Responses |
|---|---|---|
| POST | `/api/v1/tasks` | `201` `TaskResponse` + `Location: /api/v1/tasks/{id}`; `400`; `401` |
| GET | `/api/v1/tasks` | `200` `TaskResponse[]` for the caller, newest first |
| GET | `/api/v1/tasks/{id}` | `200`; `404` (also returned for another user's task, so the API does not reveal that it exists); `401` |
| GET | `/health`, `/health/ready` | as above (Tasks table) |

`userId` **never** comes from the request body or route; it always comes from the token's `sub` claim.

### 5.3 Authentication

- **Token:** JWT, HS256. Issuer `taskmanager-users`, audience `taskmanager-api`, lifetime 60 minutes. Claims: `sub` (user id), `email`, `name`.
- **Validation:** the Tasks API (and `/users/me`) validates signature, issuer, audience, and lifetime with a 30-second clock skew.
- **Key:** at least 256 bits, read from configuration (`Jwt:SigningKey`); user-secrets locally, Secrets Manager in AWS. It is never committed. Startup fails fast if it is missing or too short.
- **No account enumeration:** unknown email and wrong password return the same `401` with "Invalid email or password.". For an unknown email, a dummy hash verification still runs so response timing stays equal.
- **Rate limiting:** ASP.NET Core rate limiter, fixed window of 5 login requests per minute per client IP, returning `429` ProblemDetails.
- **No refresh tokens (YAGNI):** documented as a known limitation.
- **No email verification:** accounts are active immediately; documented as a known limitation (section 9, D5).

### 5.4 Error handling

- Services return `Result<T>` / `Result` with `Error(Code, Message, Type, FieldErrors?)`. `ErrorType` is `Validation`, `NotFound`, `Conflict`, or `Unauthorized`.
- A shared `ApiControllerBase.Problem(Error)` maps these to RFC 9457 **ProblemDetails**: `400` `ValidationProblemDetails` with per-field messages, `401`, `404`, `409`.
- Model-binding and malformed-JSON failures also return `400` ProblemDetails.
- A global `IExceptionHandler` turns unexpected exceptions into `500` ProblemDetails with `traceId`, without the stack trace outside Development, and logs them as errors.

### 5.5 Cross-cutting concerns

- **Logging:** Serilog compact JSON to stdout, with request logging and a correlation id (`X-Correlation-Id`, generated if absent). Passwords, hashes, and tokens are **never** logged.
- **Metrics (EMF):** `UserRegistered`, `LoginSucceeded`, `LoginFailed`, `TaskCreated`, under namespace `TaskManager`. They go behind an `IAppMetrics` port in Application, with an EMF implementation in Infrastructure; locally they are written to the console.
- **CORS:** only the configured Angular origin(s) (`Cors:AllowedOrigins`).
- **OpenAPI:** built-in `Microsoft.AspNetCore.OpenApi` with the Scalar UI, in Development only.
- **Lambda hosting:** `AddAWSLambdaHosting(LambdaEventSource.HttpApi)`; it has no effect when running locally.

---

## 6. Angular app (`web/`)

### 6.1 Structure (angular.dev style guide: feature folders, standalone, no `.component` suffix)

```
web/src/app/
├─ core/
│  ├─ config/    app-config.ts  (runtime /config.json via provideAppInitializer)
│  ├─ auth/      auth-store.ts, auth-api.ts, auth-interceptor.ts, auth-guard.ts, guest-guard.ts
│  └─ http/      error-interceptor.ts (ProblemDetails → AppError)
├─ layout/shell/ header (user name, sign out) + router outlet
├─ features/
│  ├─ auth/login/, auth/register/
│  └─ tasks/data/ (tasks-api.ts, tasks-store.ts, task.model.ts), task-list/, task-form/
├─ shared/ui/    form-error, empty-state, status-badge
└─ app.routes.ts lazy-loaded feature routes
```

### 6.2 State and conventions

- Signal-based stores (`AuthStore`, `TasksStore`) using `signal`, `computed`, and methods; no NgRx.
- Zoneless change detection, `OnPush`, `inject()`, `input()` / `output()`, `@if` / `@for`.
- Typed Reactive Forms whose validators mirror the backend rules. Server field errors from ProblemDetails are mapped onto the matching form controls.

### 6.3 Screens

- **`/login`** (guest only)
  - Email and password fields, a demo-credentials hint, and a "Use demo account" button that fills the form.
  - Wrong credentials: an inline alert reads "Invalid email or password.". The app stays on the page, keeps the email, clears the password, and does nothing else.
  - Success: navigates to `returnUrl` or `/tasks`.
- **`/register`** (guest only)
  - Fields: full name, email, password, confirm password.
  - A `409` shows as an error on the email field.
  - Success: the user is signed in automatically and sent to `/tasks`.
- **`/tasks`** (auth guard)
  - Add-task form: title (required), description, due date, status (default `ToDo`).
  - Task list with title, status badge, and due date.
  - Empty state: "No tasks yet — add your first one".
  - On create, the task appears at the top of the list and a "Task created" snackbar shows (Story 1 confirmation).
- **Responsive:** mobile-first CSS grid and Angular Material (M3); accessible labels and keyboard navigation.

### 6.4 Auth handling

- The token and user are kept in `sessionStorage`. On startup the app calls `/users/me`; a `401` clears the session.
- The auth interceptor attaches `Authorization: Bearer` **only** to the two configured API origins.
- The error interceptor handles `401` from a protected call by clearing the session and redirecting to `/login?returnUrl=…` with a "Your session has expired" message.
- Trade-off (documented): an httpOnly cookie would resist XSS better, but it is awkward across two separately hosted APIs.

### 6.5 Runtime configuration

`public/config.json` holds `usersApiUrl` and `tasksApiUrl`. It is loaded before bootstrap, so one build can be deployed to any environment (needed for CD in sub-project 3).

---

## 7. Testing strategy (TDD)

Every behavior starts as a failing test (red → green → refactor).

| Project | Scope | Technique |
|---|---|---|
| `*.Domain.Tests` | entities, value objects, invariants | pure unit tests |
| `*.Application.Tests` | services, validators, DTO mapping, error results, metrics calls | NSubstitute for repositories, hasher, token issuer, clock, metrics |
| `*.Infrastructure.Tests` | mappers; repositories (including the email-guard transaction and conflict path, task query by user); JWT issuer; hasher adapter; seeder idempotency | mappers as pure unit tests; repositories against DynamoDB Local via **Testcontainers** |
| `*.Api.Tests` | routes, status codes, ProblemDetails shape, `401` without or with an invalid token, `/users/me`, rate limiting, health, `userId` taken from the token | `WebApplicationFactory` with substituted services and test-generated JWTs |
| `TaskManager.ArchitectureTests` | dependency rule | NetArchTest |
| `web` | stores, interceptors, guards, login (error shown, no navigation), register, task form and list (confirmation) | Vitest + `TestBed` + `HttpTestingController` |

- **Coverage:** collected with coverlet. Domain and Application must stay at **≥ 80% line coverage** or CI fails.
- **Manual check before claiming completion:** run the full stack locally, walk every screen, and confirm the browser console has **zero warnings and errors**.
- No end-to-end browser test suite in sub-project 1 (YAGNI).

---

## 8. CI, PR review, and repository docs

### 8.1 CI: `.github/workflows/ci.yml` (on `pull_request` and push to `main`)

- **`backend` job:** setup .NET 10, restore, `dotnet build -warnaserror`, `dotnet test` with coverage and the threshold gate. Testcontainers uses the runner's Docker.
- **`frontend` job:** Node LTS, `npm ci`, `ng lint` (zero warnings), `ng test` (headless), `ng build` (production).
- **`repo-hygiene` job:** fails if any tracked file is a symbolic link (`git ls-files -s` mode `120000`).

### 8.2 Branch protection on `main`

Require a PR, all CI jobs green, and at least one **human** approval; block direct pushes; dismiss stale approvals. This is applied with `gh` **only after explicit confirmation** from the repo owner.

### 8.3 AI PR review: `.github/workflows/pr-review.yml`

- Uses `anthropics/claude-code-action` on PR open and synchronize, prompted to apply the Superpowers code-review approach and `.claude/rules/rules.md`.
- Permissions: `contents: read`, `pull-requests: write`. It can comment, but cannot push, approve, resolve, or merge.
- Authenticates to Claude with a `CLAUDE_CODE_OAUTH_TOKEN` repository secret (from `claude setup-token`, billed to the owner's Claude subscription), which the repo owner adds. GitHub access uses the job's own `GITHUB_TOKEN` (no OIDC / GitHub App exchange).

### 8.4 Documentation

- **`README.md`**
  - overview and Mermaid architecture diagram
  - prerequisites (.NET 10 SDK, Node LTS, Docker) and quick start (`docker compose up -d`, user-secrets for the JWT key, run both APIs, `npm start`)
  - demo credentials, running tests, configuration reference, API overview (with Scalar link), project layout
  - **known limitations**: no email verification, no refresh tokens, `sessionStorage` token, no E2E suite
- **`.claude/rules/rules.md`** (a real file, auto-loaded by Claude Code)
  - architecture and dependency rule; Controller → Service → Repository; no EF, Dapper, MediatR, or handler-per-use-case
  - TDD; SOLID, DRY, KISS, YAGNI
  - .NET and Angular conventions; Conventional Commits
  - **no symbolic links**
  - PR rules: CI green, AI comments only, a human resolves and merges
- **`GENAI.md`**
  - the tool (Claude Code, Claude Opus 5.5, Superpowers plugin) and the original prompt, verbatim
  - how AI output was validated: TDD, architecture tests, CI, manual browser checks, human design review
  - corrections made in this session (section 9)
  - how edge cases, authentication, and validation were handled

### 8.5 Git flow

Branch `feature/story-1-sign-in-add-task`; PR into `main` that closes #15; Conventional Commits.

---

## 9. Decision log

| # | Decision | Reason |
|---|---|---|
| D1 | Controller → Service → Repository with explicit DTOs; no handler-per-use-case | Human review: a mediator-like handler pattern is unnecessary ceremony at this scope; services keep the dependency direction and are fully testable. Split into handlers later only if services grow too many responsibilities. |
| D2 | DynamoDB + Lambda + S3/CloudFront (approach A) | Lowest cost for an assessment, "modern AWS", hosting does not leak into Domain or Application |
| D3 | Email-guard item + `TransactWriteItems` for unique email | A GSI cannot enforce uniqueness and is eventually consistent |
| D4 | Tasks keyed by `userId` + `_id` | Ownership enforced by the key; efficient listing without a GSI |
| D5 | No email verification in sub-project 1 | Amazon SES sandbox and setup friction for reviewers; not required by any story; documented limitation that can be added later as a story |
| D6 | Keep `GET /users/me` | Required authorized endpoint on the Users API; used for session validation |
| D7 | No symbolic links; enforced by CI | Human security requirement |
| D8 | `rules.md` at `.claude/rules/rules.md` | Auto-loaded by Claude Code for all collaborators; single real file |
| D9 | A due date is valid if it is not before today in the user's time zone; server accepts `dueDate >= utcToday - 1 day`, client validates against local today | The server does not know the user's time zone. Comparing with the UTC date alone rejected valid local-today dates for users ahead of UTC (their local today can be the UTC tomorrow or, near midnight, differ by a day). One day of slack covers every UTC offset (UTC-12 to UTC+14) at the cost of accepting, for users behind UTC, a date that is already past by at most one day; the client enforces the strict local rule. |

## 10. Out of scope for sub-project 1

Task update and delete (sub-project 2); AWS IaC, deployment, and CD (sub-project 3); refresh tokens; email verification; password reset; E2E test suite; pagination parameters on the list endpoint.

## 11. Carry-forward to sub-project 3 (AWS deployment)

- **Login rate limiter** is in-memory and per instance: the effective limit is multiplied by the number of instances; behind CloudFront the remote address becomes the edge IP; requests with a null IP share one bucket. Use API Gateway throttling or a WAF rate-based rule, or a trusted forwarded header.
- **EMF metric lines** written to stdout must be pure JSON: set `AWS_LAMBDA_HANDLER_LOG_FORMAT=Unformatted` or the JSON log format, and verify that metric extraction works.
- **Hosted-service seeding and table bootstrap** runs on every cold start: seed once at deploy time and keep `CreateTablesIfMissing=false`.
- **JWT signing key** must come from Secrets Manager (injected as environment variable or configuration).
- **`restoreSession`** should only clear the session on 401/404, to avoid logging users out on cold-start 5xx responses.
- **Register endpoint** has no rate limit (cost and spam exposure on Lambda).
- **`config.json`** is fetched from the root and ignores the base href.
