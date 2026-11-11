# TaskManager: my thought process

This document explains how I approached the exercise, the decisions I made along the way, and why. Setup and reference documentation are in [README.md](README.md). How I used GenAI, and how I checked and corrected it, is in [GENAI.md](GENAI.md).

**Live demo:** https://d374jkz3g8l8ht.cloudfront.net (demo credentials are in the submission email)
**Repository:** https://github.com/yamilkar/TaskManager

---

## 1. Reading the requirements

I first split the brief into three groups, because each one drives different decisions:

| Group | What it asks for | What it means for the design |
|---|---|---|
| Functional | Register, sign in, create, list, update and delete **only my own** tasks | Ownership has to be enforced on the server, not only hidden in the UI |
| Constraints | Two ASP.NET APIs, one store per entity, **no EF, Dapper or Mediator**, seeded demo data, health endpoints | Hand-written data access, and a use-case structure that doesn't depend on a mediator |
| Quality | Clean Architecture, TDD, SOLID/DRY/KISS/YAGNI, unit tests for every layer, responsive Angular, zero console errors, AWS storage and observability, CI/CD | Automated gates (architecture tests, coverage, lint, warnings as errors) instead of good intentions |

Some requirements were open to interpretation, so I made explicit calls and wrote them down:

- **"A modern AWS storage alternative"**: I chose **DynamoDB**. It's serverless, free at this scale, has a local emulator for tests, and the data model is small with known access patterns.
- **"Users and tasks stored in the same storage"**: same storage technology, but **one table per API**. Each API owns its data, and they share only the `SharedKernel` project and a JWT.
- **"Authorized and non-authorized endpoints"**: register, login and health are public. `GET /users/me` and every task endpoint require a token.

## 2. Breaking the work into user stories

Rather than build "the backend" and then "the frontend", I wrote **user stories with acceptance criteria** on a GitHub Project board. I delivered each one end to end (API, UI, tests) on its own branch and pull request:

| Story | Outcome | PR |
|---|---|---|
| Foundation + Story 1: sign in and add my first task | Solution skeleton, both APIs, DynamoDB, JWT, Angular shell, CI | #29 (+ #30, #31) |
| Story 2: see my tasks | Newest-first list | #32 |
| Story 3: describe my task fully | Description, status, due date | #33 |
| Story 4: update a task | Edit it and move it between statuses | #35 |
| Story 5: delete a task | Delete with confirmation | #36 |
| Story 6: create my own account | Registration UI and rules | #37 |
| Sign out (#34) | Sign out, session-expiry handling | Delivered with the foundation |
| Story 9: use it over the internet | AWS deployment and continuous deployment | #39, #40 |
| Story 8: use it on my phone | Responsive status board, light and dark theme | #41 |

Why I worked this way: every merge leaves a working, demonstrable product. Review happens in small, focused diffs. And if time had run out, what exists is complete rather than half of every layer.

Each story followed the same loop:

1. **Brainstorm** the story and its edge cases.
2. Write a **design spec** (`docs/superpowers/specs/`) and an **implementation plan** (`docs/superpowers/plans/`).
3. Implement each task with **TDD**: a failing test, the minimum code to pass, then refactor.
4. **Review** every task (spec compliance and code quality), then an automated AI review on the PR, then my own review.
5. Merge only when CI is green (`backend`, `frontend`, `repo-hygiene`).

## 3. Key decisions and trade-offs

### Architecture: Clean Architecture with CQRS-style handlers and no mediator

Each API has `Domain → Application → Infrastructure → Api` layers. Every use case is one command or query record with **one handler** (`RegisterUserHandler`, `CreateTaskHandler`, ...). Controllers inject the handler they need directly.

- My first design used a `Controller → Service → Repository` structure. During review I moved to one handler per use case. A `TaskService` with five methods has five reasons to change; one handler per use case has one, and each is easy to test on its own.
- Injecting handlers directly gives the main benefit of a mediator (small, isolated use cases) without the indirection, and it respects the "no Mediator" constraint.
- **NetArchTest** tests enforce the rules: Domain and Application can't reference AWS or ASP.NET, only the Api references Infrastructure, and every request body is an Api-layer DTO. Architecture that's only documented tends to erode; architecture that's tested doesn't.

### Data access: hand-written DynamoDB repositories

- **Tasks table** key = `userId` + `_id`. Ownership is part of the key, so another user's task can't be read even by mistake. It returns `404`, not `403`, so the API doesn't reveal that the task exists.
- **Users table** stores a user item plus an `EMAIL#<normalized email>` guard item, written together in one **transaction**. That's the only way to guarantee unique emails under concurrent registrations in DynamoDB. A secondary index can't enforce uniqueness and is eventually consistent.
- Repository tests run against **DynamoDB Local in Testcontainers**, so the data layer is tested against real DynamoDB behavior, not mocks.

### Security, by default rather than as an afterthought

- PBKDF2 password hashing. Wrong email and wrong password return the **same `401`**, with a dummy hash check so the response time doesn't reveal whether an account exists.
- JWT pinned to HS256, with issuer and audience validation, and a signing key of at least 32 bytes (the API refuses to start otherwise). `userId` always comes from the token's `sub` claim, never from the request.
- Rate limiting on login and register. Passwords, hashes and tokens are never logged.
- The client sends the token only to the two exact API origins.
- No secrets in the repository: user-secrets locally, and SSM SecureStrings plus GitHub OIDC in AWS (no AWS keys in GitHub).

### Validation in both places

The domain and the Application validators are the source of truth. The Angular forms mirror the same rules for instant feedback, and the server's field errors map back onto the form controls. Malformed JSON, unknown statuses and missing bodies return `400` ProblemDetails, never `500`.

### AWS: designed for $0 a month

I treated cost as a requirement:

- **Lambda Function URLs** instead of API Gateway.
- **DynamoDB** provisioned at 5/5 capacity per table, inside the always-free tier.
- **S3 + CloudFront** for the Angular app.
- **SSM Parameter Store** instead of Secrets Manager.
- **CloudWatch** JSON logs with correlation ids, and Embedded Metric Format metrics.

The infrastructure is AWS CDK in C# and has its own tests. CI deploys only the artifacts that changed since the last successful deployment, then smoke-tests the live site.

### KISS and YAGNI: what I deliberately left out

- **Pagination**: I built it once (cursor-based through a secondary index), then removed it. No story needed it and it added real complexity. The list is unpaged, newest first.
- **Refresh tokens, email verification, password reset**: not required. They're documented as known limitations with the reasoning.
- **Validation and mapping libraries**: hand-written validators and mappers are short, explicit and easy to test.

## 4. How I kept quality high

- **TDD throughout**: about 250 .NET tests (domain, application, repositories, API endpoints, architecture, infrastructure) and about 150 Angular tests.
- **CI gates on every PR**:
  - `dotnet build -warnaserror`;
  - all tests;
  - **80% line coverage minimum on Domain + Application** (measured at about 98%);
  - `ng lint --max-warnings 0`;
  - a check that fails on any symbolic link.
- **Edge cases with tests**:
  - email case and whitespace;
  - cross-user access;
  - due dates at the UTC day boundary;
  - malformed input;
  - concurrent registrations with the same email;
  - telling a failed login apart from an expired session in the UI.
- **Collaborator rules** in `.claude/rules/rules.md`, used both by people and by the AI PR reviewer. The reviewer only comments; I resolve every thread and merge.
- **Manual walkthroughs** in the browser, on desktop and at phone width, checking the console stays clean.

## 5. Problems I hit and how I solved them

| Problem | Fix |
|---|---|
| The correlation id disappeared from `500` responses | Middleware order: the correlation middleware now runs before the exception handler |
| Two people registering the same email at the same time could get a DynamoDB transaction conflict | Retry in the repository, which still guarantees exactly one winner (`409` for the other) |
| Token could be sent to a look-alike origin (prefix match) | Exact origin match only |
| CI could deploy from a feature branch that edits the workflow | The deploy role trusts only OIDC tokens from `main` in the `production` environment, so it fails closed |
| The demo user's password must work in AWS but can't be public | A private password in SSM, seeded at cold start; the public `Demo123!` is rejected outside Development |

## 6. What I would do next

- **"Keep my tasks private"** (#21): its acceptance criteria are already met by existing behavior (key-level ownership, auth on every task endpoint, sign-out, session expiry). It needs a formal acceptance pass and to be closed.
- **"Focus on what matters"** (#22): the new status board already groups tasks by status. Still to do: an "all / one status" filter and a soonest-due-first sort.
- An **end-to-end browser test suite** (Playwright) on top of the unit and integration tests.
- An **httpOnly cookie** instead of `sessionStorage` for the token, plus refresh tokens, if sessions need to last longer.
- A custom domain, WAF and a shared rate-limit store, once it's worth leaving the free tier.
