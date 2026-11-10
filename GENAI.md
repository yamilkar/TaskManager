# GenAI usage

## Tool

**Claude Code** (model Claude Opus 5.5) with the **Superpowers** plugin, used as a workflow: brainstorming, then a written design spec, then an implementation plan, then subagent-driven TDD execution with an independent spec and quality review after every task, then a final review. A human reviewed the design and made the key architectural calls.

## Original prompt

The first prompt, verbatim:

~~~text
I want to design and implement an application for task management, it should allow users to register, login, create tasks, update, delete and list their tasks only.

I want to start implementing first the signing and task creation as described here:
https://github.com/users/yamilkar/projects/9/views/1?pane=issue&itemId=298896980&issue=yamilkar%7CTaskManager%7C15

The main components are: Tasks API and Users API, tasks and users are stored in the same storage, the client UI application.

Architecture guidelines: Adhere to Clean Architecture principles, including separation of concerns and independence of components and use Test-Driven Development (TDD).
The code should be well-organized, readable, and adhere to best practices, apply SOLID, DRY, KISS, and YAGNI.

Technical Requirements:
Database: Create a database or other data storage, cannot use Entity Framework, Dapper, or Mediator.
One table/object/container to store tasks and another for users.
Task: _id(Guid), userId, title, description, status, due_date, createdAt, updatedAt
User: _id (Guid), full_name, email, passwordHash, createdAt
The application should have seeded data and credentials for demo purposes
I want to use a modern AWS storage alternative for persistence and AWS options for logging and metrics.

APIs:
Create an ASP.NET Web API with endpoints that allow users to perform CRUD
operations on the tasks data and another API for user creation, user login,
and authorized and non-authorized endpoints.
Both APIs will have health endpoints.
Data layer:
Develop a data access layer that interacts with the data and provides the
necessary CRUD operations for the API endpoints.
Business logic layer:
Develop a business logic layer that includes all of the business rules and
validation for the application.
This layer should be independent of the data layer and the API.
The applications should perform as expected in the requirements without
errors or bugs.

Unit tests:
Write unit tests for all components of the applications, including the data access
layer, business logic layer, and API endpoints.

Frontend: 
Will be supported by an Angular app integrated with both APIs.
The frontend should be responsive and user-friendly.
Implement CRUD operations associated with the implemented use case.
Structured code: Organize your components and state cleanly.
Refer to https://angular.dev/style-guide for best practices.
No warnings/errors in the browser console

Instructions:
Include a README with setup instructions and any other documentation you
deem necessary.

Include a Claude/Rules folder with rules.md file including the rules described, so other collaborators follow same style.
Superpowers will review the PRs with fresh eyes and add comments, but cannot resolve or merge before human confirmation.

Create a GENAI.md file at root level with:
The GenAI tool used for the project.
The main first prompt used to generate the apps, actually this prompt.
And describe how you:
Validated the AI's suggestions
Corrected or improved the output, if necessary
Handled edge cases, authentication, or validations

I want to explore options to deploy the APIs and the UI within AWS.
I want to create a CI/CD workflow using GitHub tools to continuous deploy new code. 
All tests should pass before merging.
~~~

## How the AI's output was validated

- **Test-driven development**: every task began with a failing test (RED), then the minimum code to pass (GREEN).
- **Independent per-task review**: each task got a separate AI review for spec compliance and code quality, with fix loops until clean.
- **Architecture tests** (NetArchTest) enforce the dependency rule; Domain and Application reference no AWS or ASP.NET packages.
- **CI gates**: `dotnet build -warnaserror`, all tests, a line-coverage gate of 80% on Domain and Application (measured 98.4%), `ng lint --max-warnings 0`, and a symlink guard.
- **Manual browser walkthrough** of the full stack:
  - wrong password shows an alert, keeps the email, clears the password, and does not navigate;
  - the demo login shows the seeded tasks;
  - adding a title-only task shows "Task created" and puts it first in the list;
  - a refresh keeps the session; sign out clears it;
  - a newly registered user sees only their own tasks;
  - the 375px layout works.
- **Human design review** of the spec before any code was written.

## How the output was corrected or improved

Human-driven, during design:

- During design, replaced a mediator-style handler design with **Controller → Service → Repository** and DTOs (less ceremony, still testable).
- During PR review, the human changed direction to **CQRS-style use cases**: one command or query handler per use case (`ICommandHandler`/`IQueryHandler`), injected directly into controllers, still with **no mediator**. Each use case now has one class with one reason to change, and architecture tests enforce the rule.
- Also during PR review, the human asked to **cut boilerplate**: removed the `AssemblyMarker` classes (architecture tests load assemblies through real types), the Angular CLI scaffolding (`web/README.md`, `web/.vscode/`), merged `web/.gitignore` and `web/.editorconfig` into the root files, moved the Prettier config into `package.json`, and dropped `appsettings.json` keys that only repeated code defaults. `coverlet.runsettings` was kept: without it, generated regex code counts toward coverage.
- The AI PR review's comments were then addressed: request DTOs in the Users API (no binding of Application commands), a rate limit on register, a stricter status parser, the D9 comment reference, and spec section 11 notes on forwarded headers and the unbounded task list. An architecture test now checks that every request body is an Api-layer DTO.
- **Email verification scoped out** because of the Amazon SES sandbox, and documented as a limitation.
- **`/users/me` kept** as the Users API's authorized endpoint and for validating a session on startup.
- Asked for an explanation of the **email-guard transaction versus a GSI**; a GSI cannot enforce uniqueness and is eventually consistent, so the guard item stays.
- **No symbolic links**, enforced by a CI job.
- The **Docker upgrade** was decided by the human; Testcontainers stays the default, with a `DYNAMODB_TEST_ENDPOINT` override for older Docker.

AI-review-driven fixes during execution:

- The **correlation id was lost on 500 responses** (middleware order); `CorrelationIdMiddleware` now runs before the exception handler.
- **JWT options were read eagerly**, which broke test overrides; they are now bound lazily with validation on start.
- **HS256 was not pinned**; the accepted algorithm is now restricted to HS256.
- The **bearer token leaked to look-alike origins** through a string prefix match; it is now sent only on an exact origin match.
- **Concurrent registration** could fail with `TransactionConflict`; the repository now retries and still returns exactly one winner.
- The **PR-review workflow lacked the `id-token` permission** that `claude-code-action` needs for its OIDC → GitHub App token exchange. The human reviewer then chose the more secure option: pass the job's own `GITHUB_TOKEN` (least privilege, no third-party token minting) and drop `id-token: write`, and to authenticate Claude with a subscription token (`CLAUDE_CODE_OAUTH_TOKEN`) instead of a pay-as-you-go API key.

A known and accepted item (console): the only console entry seen is the browser's own "Failed to load resource: 401" for the intentional wrong-password attempt. It is emitted by the browser, not by app code, and cannot be suppressed without breaking the API contract (see README, Known limitations).

## How edge cases, authentication and validation were handled

The five review-focus items, and where each is tested:

| # | Edge case | Behavior | Tested in |
|---|---|---|---|
| 1 | Email case and whitespace | ` Ana@X.com ` registers; `ana@x.com` logs in to the same account; `ANA@x.com` then gets `409` | `Email` (Domain) and `RegisterUserHandler`/`LoginHandler` (Application) tests |
| 2 | Cross-user access | user A's token requesting user B's task id gets `404`, never B's data (the key is `userId` + `_id`) | `GetTaskHandler` tests and Tasks API tests |
| 3 | Due date at the UTC boundary | today (UTC) is accepted, yesterday is rejected | Tasks Application tests with a fixed clock |
| 4 | Malformed input | malformed JSON, a missing body or an unknown `status` return `400` ProblemDetails, never `500` | Users API and Tasks API tests |
| 5 | Login 401 versus session expiry | a login `401` shows "Invalid email or password." with no "session expired" redirect; a Tasks API `401` triggers it | Angular `errorInterceptor` tests |

Authentication and validation, in short:

- JWT HS256 with a signing key of at least 32 bytes (startup fails otherwise), issuer and audience checks, and a 30 second clock skew.
- Passwords are hashed with PBKDF2 and never logged. Unknown email and wrong password return an identical `401`, with a dummy hash check to equalize timing. Login and register are rate-limited (5 per minute per IP each, separate buckets).
- Unique email is guaranteed by the guard item in a single transaction, including under concurrent registration.
- Validation exists on both sides: domain invariants and Application validators on the server, and typed form validators that mirror them in Angular, with server field errors mapped onto the form controls.
- The client sends the bearer token only to the two configured API origins.
