# Story 3: Describe My Task Fully — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close yamilkar/TaskManager#17 by making the past-due-date message tell the user what to do (on the spot), aligning client length checks with the server, and pinning all three acceptance criteria with Api-, Application- and form-level tests plus acceptance evidence.

**Architecture:** No new endpoint, field or storage change (spec D16). One domain constant carries the past-date text; the Angular `TaskForm` mirrors it. Everything else is tests and evidence over the existing `POST /api/v1/tasks` path (Controller → `CreateTaskHandler` → `ITaskRepository`).

**Tech Stack:** .NET 10, xUnit, NSubstitute, Shouldly, `WebApplicationFactory`; Angular 22 (zoneless, signals, OnPush), Angular Material, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-10-story-3-describe-my-task-design.md`

> **Before starting:** the spec's open questions Q1–Q3 (section 8) must be answered. This plan implements the recommendations (keep the status select; new wording; keep D9). If Q2 is answered "keep the current wording", skip the copy change in Tasks 1 and 4 but keep the tests, asserting `"Due date cannot be in the past."`. If Q1 is answered "strict", add a Task 0 that removes `Status` from `CreateTaskRequest`/`CreateTaskCommand` and the form select — and update the spec first.

## Global Constraints

- Past-date message, byte-identical on server and client: `Due date cannot be in the past. Choose today or a later date.`
- Due-date hint text: `Today or later.`
- Status default `ToDo`, label "To do". Title 1–200 chars trimmed; description ≤ 2000 chars trimmed, blank → `null`.
- Server due-date rule stays D9: `dueDate >= utcToday − 1 day` (do not change `TaskItem.Create`'s comparison).
- Do **not** touch `web/src/app/features/tasks/task-list/`, `tasks-store.ts`, `tasks-page/`, `ListTasks/`, or `GET /api/v1/tasks` (Story 2 is changing them on `feature/story-2-see-my-tasks`).
- Test names `Method_condition_expected` (C#) / `TaskForm: <behaviour>` (Vitest). `dotnet build -warnaserror` clean; `ng lint --max-warnings 0`; zero browser console warnings or errors.
- Conventional Commits, each ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Branch `feature/story-3-describe-my-task`.

## Review Focus

- **Past date typed, not picked:** a user typing `01/01/2000` into the native date input (the `min` attribute does not stop typing) must see the D18 message before pressing "Add task" → Task 4 test `TaskForm: shows the past-date reason as soon as a past date is entered`.
- **Trailing whitespace at the length limit:** 2000 description chars plus a trailing newline (pasted text) must be accepted, as the server trims → Task 4 tests `TaskForm: accepts 2000-char description with surrounding whitespace` and the title equivalent.
- **Client and server texts drift:** someone edits one message and not the other → literal-text assertions in Task 1 (domain) and Task 4 (form) against the same Global Constraints string.
- **Status omitted vs. sent as empty string:** an API client sending `"status": ""` gets `400` (empty is not "missing"); only an absent/`null` status defaults to `ToDo` → Task 3 test `Create_with_empty_status_returns_400_with_status_error`.
- **Clearing a previously invalid date:** after a past-date error, clearing the field must remove the error and allow submit (no due date is valid) → Task 4 test `TaskForm: clearing a past due date clears the error`.

---

### Task 1: Domain — past-date message tells the user what to do

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Domain/TaskItem.cs` (constants block; the `dueDate` error line)
- Test: `tests/TaskManager.Tasks.Domain.Tests/TaskItemTests.cs`

**Interfaces:**
- Produces: `public const string TaskItem.DueDateInPastMessage` = the Global Constraints text. Tasks 2 and 3 assert against it.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Create_pastDueDateMessage_tellsTheUserWhatToDo()
{
    var now = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    var result = TaskItem.Create(Id, UserId, "t", null, null, new DateOnly(2026, 11, 8), now);

    result.Error!.FieldErrors!["dueDate"].ShouldBe(["Due date cannot be in the past. Choose today or a later date."]);
    TaskItem.DueDateInPastMessage.ShouldBe("Due date cannot be in the past. Choose today or a later date.");
}
```

Also change the existing assertion in `Create_rejects_due_date_two_days_before_today_utc` to `ShouldContain(TaskItem.DueDateInPastMessage)`.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TaskManager.Tasks.Domain.Tests --filter "FullyQualifiedName~TaskItemTests"`
Expected: FAIL — build error, `DueDateInPastMessage` not defined.

- [ ] **Step 3: Add `DueDateInPastMessage` next to `TitleMaxLength` and use it for `errors["dueDate"]`**

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/TaskManager.Tasks.Domain.Tests`
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add src/Tasks/TaskManager.Tasks.Domain/TaskItem.cs tests/TaskManager.Tasks.Domain.Tests/TaskItemTests.cs
git commit -m "feat: tell the user to pick today or later for a past due date"
```

### Task 2: Application — pin past date and full-field create in the handler

Characterisation tests: the behaviour exists, so a green first run is expected. Verify each fails when its assertion is deliberately inverted, then restore.

**Files:**
- Test: `tests/TaskManager.Tasks.Application.Tests/CreateTaskHandlerTests.cs` (uses the existing `Now = 2026-11-10T12:00Z`, `Command(...)` helper, `_repo`)

**Interfaces:**
- Consumes: `TaskItem.DueDateInPastMessage` (Task 1); `CreateTaskCommand(Guid UserId, string? Title, string? Description, string? Status, DateOnly? DueDate)`.

- [ ] **Step 1: Write the tests**

- `HandleAsync_pastDueDate_returnsDueDateErrorAndDoesNotPersist`: `Command("t", dueDate: new DateOnly(2026, 11, 8))` → `IsFailure`, `Error.Type == ErrorType.Validation`, `FieldErrors["dueDate"]` contains `TaskItem.DueDateInPastMessage`; `_repo.DidNotReceive().AddAsync(...)`.
- `HandleAsync_descriptionStatusAndDueDate_areReturnedInResponse`: `Command("t", "Done", new DateOnly(2026, 12, 1), "  notes ")` → `Description == "notes"`, `Status == "Done"`, `DueDate == new DateOnly(2026, 12, 1)`.

- [ ] **Step 2: Run, then invert one assertion in each to watch it fail, then restore**

Run: `dotnet test tests/TaskManager.Tasks.Application.Tests --filter "FullyQualifiedName~CreateTaskHandlerTests"`
Expected: PASS (and FAIL while inverted).

- [ ] **Step 3: Commit**

```bash
git add tests/TaskManager.Tasks.Application.Tests/CreateTaskHandlerTests.cs
git commit -m "test: pin past due date and full-field create in CreateTaskHandler"
```

### Task 3: Api — acceptance criteria at the HTTP boundary

Characterisation tests over **real handlers** (`new TasksApiFactory(useRealHandlers: true)`), using the existing `ClientWith(factory, userId)`, `Json(...)` and `Url` helpers in `TasksEndpointsTests`. Dates: past `2000-01-01`, future `2099-12-31` (independent of the real clock).

**Files:**
- Test: `tests/TaskManager.Tasks.Api.Tests/TasksEndpointsTests.cs`

**Interfaces:**
- Consumes: `TaskItem.DueDateInPastMessage` (Task 1). Field keys in `errors` are camelCase (`title`, `status`, `dueDate`).

- [ ] **Step 1: Write the tests**

| Test | Body | Expected |
|---|---|---|
| `Create_without_title_returns_400_with_title_error` | `{"description":"d"}` | `400`, `application/problem+json`, `errors.title[0] == "Title is required."` |
| `Create_without_status_returns_201_with_status_ToDo` | `{"title":"x"}` | `201`, body `status == "ToDo"` |
| `Create_with_empty_status_returns_400_with_status_error` | `{"title":"x","status":""}` | `400`, `errors.status` present |
| `Create_with_past_due_date_returns_400_with_reason` | `{"title":"x","dueDate":"2000-01-01"}` | `400`, `errors.dueDate[0] == TaskItem.DueDateInPastMessage` |
| `Create_with_description_status_and_due_date_returns_201_with_all_fields` | `{"title":"x","description":"d","status":"InProgress","dueDate":"2099-12-31"}` | `201`, body `description == "d"`, `status == "InProgress"`, `dueDate == "2099-12-31"` |

`TaskManager.Tasks.Domain` reaches this project transitively (Api.Tests → Tasks.Api → Application → Domain), so add `using TaskManager.Tasks.Domain;` and assert against `TaskItem.DueDateInPastMessage`. Add no new project reference.

- [ ] **Step 2: Run, invert-to-fail check, restore**

Run: `dotnet test tests/TaskManager.Tasks.Api.Tests --filter "FullyQualifiedName~TasksEndpointsTests"`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/TaskManager.Tasks.Api.Tests/TasksEndpointsTests.cs
git commit -m "test: cover Story 3 acceptance criteria at the Tasks API boundary"
```

### Task 4: Angular `TaskForm` — reason on the spot, hint, trimmed lengths

**Files:**
- Modify: `web/src/app/features/tasks/task-form/task-form.ts`, `task-form.html`
- Test: `web/src/app/features/tasks/task-form/task-form.spec.ts` (existing helpers `q`, `type`, `submit`)

**Interfaces:**
- Produces: `export const DUE_DATE_IN_PAST_MESSAGE = 'Due date cannot be in the past. Choose today or a later date.';` and `function maxTrimmedLength(max: number): ValidatorFn` returning `{ maxlength: { requiredLength: max, actualLength } }` when `value.trim().length > max` (same key as `Validators.maxLength`, so template branches stay).

- [ ] **Step 1: Write the failing tests**

- `TaskForm: status defaults to To do` — `q<HTMLSelectElement>('select[name=status]')` has `value === 'ToDo'` and its selected option text `'To do'`. *(characterisation)*
- `TaskForm: shows the past-date reason as soon as a past date is entered` — type `2000-01-01` into `input[name=dueDate]`, `fixture.detectChanges()`, **no submit** → `el.textContent` contains `DUE_DATE_IN_PAST_MESSAGE`.
- `TaskForm: past-date message matches the server text` — `expect(DUE_DATE_IN_PAST_MESSAGE).toBe('Due date cannot be in the past. Choose today or a later date.')`.
- `TaskForm: due date shows the today-or-later hint` — `el.querySelector('mat-hint')!.textContent` contains `'Today or later.'`.
- `TaskForm: clearing a past due date clears the error` — type `2000-01-01`, then `''`, fill title, submit → one emit with `dueDate: null`, message absent.
- `TaskForm: accepts 2000-char description with surrounding whitespace` — description `'  ' + 'a'.repeat(2000) + '\n'` → one emit, `description.length === 2000`.
- `TaskForm: accepts 200-char title with surrounding whitespace` — likewise for title → `title.length === 200`.
- `TaskForm: reset leaves the due date untouched`: type `2099-12-31`, call `reset()`, then `expect(fixture.componentInstance['form'].controls.dueDate.touched).toBe(false)`. This guards the on-change touch below, because `FormControl.reset` emits `valueChanges` after it marks the control untouched.
- Update `TaskForm: rejects a due date before local today with the server message` to expect `DUE_DATE_IN_PAST_MESSAGE`.

- [ ] **Step 2: Run to verify the new-behaviour tests fail**

Run: `cd web && npx ng test --watch=false --include src/app/features/tasks/task-form/task-form.spec.ts`
Expected: FAIL — `DUE_DATE_IN_PAST_MESSAGE` not exported; no hint; whitespace-padded lengths rejected.

- [ ] **Step 3: Implement in `task-form.ts` / `task-form.html`**

- Export the constant and `maxTrimmedLength`; use `maxTrimmedLength(200)` / `(2000)` in place of `Validators.maxLength`.
- In the constructor, subscribe to `form.controls.dueDate.valueChanges` with `takeUntilDestroyed()` (from `@angular/core/rxjs-interop`). When the new value is a non-empty string, call `markAsTouched()` and `cdr.markForCheck()`, so the `pastDate` error renders without a submit. The non-empty guard keeps `reset()` from leaving the control touched.
- Expose `protected readonly dueDateInPastMessage = DUE_DATE_IN_PAST_MESSAGE;` and render it in the `pastDate` `<mat-error>`; add `<mat-hint>Today or later.</mat-hint>` inside the due-date `mat-form-field`; import `MatHint`.

- [ ] **Step 4: Run tests and lint**

Run: `cd web && npx ng test --watch=false && npx ng lint --max-warnings 0`
Expected: all specs PASS, lint clean.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/features/tasks/task-form
git commit -m "feat: show why a past due date is rejected as soon as it is entered"
```

### Task 5: Acceptance evidence and README

**Files:**
- Modify: `README.md` (the capability/status table at the top)

- [ ] **Step 1: Full verification**

Run: `dotnet build TaskManager.slnx -warnaserror` then `dotnet test TaskManager.slnx` (Docker running for Testcontainers) and `cd web && npm test -- --watch=false && npx ng lint --max-warnings 0 && npx ng build`
Expected: all green; Domain and Application coverage ≥ 80%.

- [ ] **Step 2: Manual acceptance walk-through** (preview configs `users-api`, `tasks-api`, `web` in `.claude/launch.json`; sign in with the demo account)

Perform the three manual checks in spec section 7, take one screenshot per AC, and confirm `read_console_messages` shows zero warnings and errors on `/tasks`.

- [ ] **Step 3: README "Project status"**: add the row `| Describe a task (description, status, due date) | Done (Story 3) |` below `| Create, list, get a task | Done |`, and add Story 3 to the intro sentence above the table (line 5, "This repository delivers …"). If Story 2 (`feature/story-2-see-my-tasks`) has merged a different status layout by then, rebase first and follow that layout.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: mark Story 3 done in the project status"
```

The PR (closes #17) carries the spec section 7 evidence table filled in with test names and the three screenshots.
