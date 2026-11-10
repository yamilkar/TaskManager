# Design Spec — Story 3: Describe my task fully

- **Date:** 2026-11-10
- **Status:** Draft — awaiting human review (open questions in section 8)
- **Issue:** yamilkar/TaskManager#17 — *Story 3: Describe my task fully*
- **Builds on:** [Story 1 foundation spec](2026-11-10-story-1-foundation-design.md) (field rules in 4.3, decision D9 for due dates). Story 2 (#16, paged task list) uses decisions D11–D15; this spec continues at D16.

---

## 1. Story and acceptance criteria

**As** a user, **I want** to give a task a description, a status and a due date, **so that** I know what it is, where it stands and when it's due.

- AC1: A task always needs a title.
- AC2: New tasks start as "To do".
- AC3: I can't set a due date in the past, and I'm told why.

## 2. What already exists on `main`

Sub-project 1 built the full task model, so Story 3 needs no new endpoint, no new field and no storage change.

| Layer | Already there |
|---|---|
| Domain | `TaskItem.Create` trims the title and requires 1–200 chars, trims the description (blank → `null`, ≤ 2000 chars), parses status with `TaskItemStatuses.TryParse` (missing → `ToDo`, case-insensitive names only), rejects `dueDate < utcToday − 1 day` (D9) with `"Due date cannot be in the past."`, and returns every field error in one `Result` |
| Application | `CreateTaskHandler` delegates all rules to the domain, persists, increments `TaskCreated` |
| Api | `POST /api/v1/tasks` binds `CreateTaskRequest(Title, Description, Status, DueDate)`; validation errors become `400` `ValidationProblemDetails` with camelCase field keys; a non-`yyyy-MM-dd` date is a model-binding `400` |
| Storage | `description`, `status`, `due_date` are stored and read back (mapper round-trip test) |
| Angular | `TaskForm` has title, description (textarea), status (native select, default `ToDo`, labels "To do" / "In progress" / "Done") and due date (`type="date"`, `min` = local today, `notBeforeLocalToday` validator); server field errors are shown under the matching control; `TaskList` shows description, status badge and due date |

## 3. Scope per acceptance criterion

| AC | Already covered | Missing (this story) |
|---|---|---|
| AC1 title required | Domain, handler (`HandleAsync_blankTitle_…`), form (`required`, `notBlank`) | An **Api-level** test that a body without a title returns `400` with an `errors.title` entry; the form's max-length check counts the untrimmed text while the server trims (D20) |
| AC2 starts as "To do" | Domain default; form default `ToDo`; badge label "To do" | An **Api-level** test that a body without `status` returns `201` with `"status":"ToDo"`; a form test that the status select shows "To do" selected by default |
| AC3 no past due date, told why | Domain rule (D9) and message; form validator, `min`, message; server message rendered under the field | The message says *what* is wrong but not *what to do* (D18); the client message appears only after submit or blur, not when the date is picked or typed (D18); no handler or **Api-level** test for a past date; no test that the client and server texts are identical |
| All | — | Acceptance evidence (section 7) and a README "Project status" line for Story 3 |

## 4. Decisions

| # | Decision | Reason |
|---|---|---|
| D16 | Story 3 changes no contract: same `POST /api/v1/tasks` body and response, same table attributes | Every field already exists end to end; YAGNI |
| D17 | `status` stays **optional on create and defaults to `ToDo`**; the form keeps the status select, preset to "To do" | The story asks to *give* a task a status; update is Story 4, so removing the choice would leave no way to set it until then. AC2 is read as "a task created without a status is To do". **Open question Q1.** |
| D18 | One past-date message, identical on server and client: **"Due date cannot be in the past. Choose today or a later date."** It lives in `TaskItem.DueDateInPastMessage` and in one Angular constant. The client marks the due-date control as touched on `change`, so the message shows as soon as a past date is picked or typed, not only on submit | AC3 says "I'm told why": the reason plus the way out, at the moment of the mistake. Keeping one text avoids two wordings for the same rule. **Open question Q2** for the wording |
| D19 | Keep D9 unchanged: the server accepts `dueDate >= utcToday − 1 day`; the client enforces local today | The server has no time zone. Sending one (header or body field) is a contract change for a ≤ 1-day gap that only a direct API caller behind UTC can reach. **Open question Q3** |
| D20 | The form's title and description length checks count the **trimmed** value (`maxTrimmedLength(n)` validator), matching the domain | Today a 2000-char description with a trailing newline is rejected by the client but accepted by the server; the client must never be stricter than the rule it mirrors |

## 5. Changes

### 5.1 Backend

- `TaskItem`: add `public const string DueDateInPastMessage = "Due date cannot be in the past. Choose today or a later date.";` and use it for the `dueDate` error. No other domain change.
- No change to `CreateTaskHandler`, `TasksController`, the repository, mapper or seeder.

### 5.2 Angular (`web/src/app/features/tasks/task-form/` only)

- `task-form.ts`: export `DUE_DATE_IN_PAST_MESSAGE` with the D18 text; replace `Validators.maxLength(200)` / `(2000)` with `maxTrimmedLength(200)` / `(2000)` (same `maxlength` error key so the template messages stay); on the due-date control's value change, mark it touched so the error renders immediately.
- `task-form.html`: render `{{ dueDateInPastMessage }}` instead of the literal; add a `<mat-hint>Today or later.</mat-hint>` under the due date (Material links it to the input for screen readers and hides it while an error shows) so the rule is visible before the user makes a mistake.
- Not touched (Story 2 owns them): `task-list/`, `tasks-store.ts`, `tasks-page/`, `GET /api/v1/tasks`.

## 6. Testing per layer (TDD)

| Project | New or changed tests |
|---|---|
| `Tasks.Domain.Tests` | `Create_rejects_due_date_two_days_before_today_utc` asserts `TaskItem.DueDateInPastMessage`; `Create_pastDueDateMessage_tellsTheUserWhatToDo` pins the exact text |
| `Tasks.Application.Tests` | `HandleAsync_pastDueDate_returnsDueDateErrorAndDoesNotPersist`; `HandleAsync_descriptionStatusAndDueDate_areReturnedInResponse` |
| `Tasks.Api.Tests` (real handlers over a substituted repository) | `Create_without_title_returns_400_with_title_error`; `Create_without_status_returns_201_with_status_ToDo`; `Create_with_empty_status_returns_400_with_status_error`; `Create_with_past_due_date_returns_400_with_reason`; `Create_with_description_status_and_due_date_returns_201_with_all_fields` |
| `web` `task-form.spec.ts` | default status select shows "To do"; past date shows the D18 text **without submitting**; the "Today or later." hint is visible; clearing a past date clears the error; 2000 chars plus surrounding whitespace is accepted (title 200 likewise); client constant equals the server text (literal assertion in both suites) |

Characterisation tests (the behaviour already exists) are expected to pass on first run; the plan marks them so a reviewer does not read a green first run as a TDD slip. New behaviour (D18 copy, D18 on-change display, D20) goes red first.

Coverage gate (Domain and Application ≥ 80%) is unaffected or rises.

## 7. Acceptance evidence

| AC | Automated evidence | Manual check (local stack, demo account) |
|---|---|---|
| AC1 | `Create_without_title_returns_400_with_title_error`, `TaskForm: title is required`, `TaskForm: whitespace-only title is rejected` | Submit an empty form → "Title is required." under Title, nothing created |
| AC2 | `Create_without_status_returns_201_with_status_ToDo`, `HandleAsync_validCommand_persistsTaskOwnedByCaller` (existing, asserts `ToDo`), `TaskForm: status defaults to To do` | Open the form → Status shows "To do"; add a task without touching it → list badge "To do" |
| AC3 | `Create_with_past_due_date_returns_400_with_reason`, `HandleAsync_pastDueDate_…`, `TaskForm: shows the past-date reason as soon as a past date is entered` | Type yesterday's date → "Due date cannot be in the past. Choose today or a later date." appears before submit; submit is blocked; today is accepted |
| All | CI green (`backend`, `frontend`, `repo-hygiene`) | Browser console has zero warnings and errors on `/tasks`; screenshots of the three checks attached to the PR |

## 8. Open questions for the user

1. **Q1 — Status choice on create (D17).** Recommendation: keep the select, defaulting to "To do". Alternative: remove `status` from the create form and have the server always create `ToDo` (strict reading of AC2); status would then only change through Story 4's edit.
2. **Q2 — Past-date wording (D18).** Recommendation: "Due date cannot be in the past. Choose today or a later date." Alternative: keep the current "Due date cannot be in the past." (already tells *why*; fewer test changes).
3. **Q3 — Server-side one-day tolerance (D19).** Recommendation: keep D9. Alternative: the client sends its local date (for example `X-Client-Date`) and the server checks against it when present; stricter, but a contract change and a value the client controls anyway.

## 9. Out of scope

Editing description, status or due date of an existing task (Story 4); deleting a task (Story 5); filtering or sorting by status or due date (Story 8); the task list, store paging and `GET /api/v1/tasks` (Story 2); maximum due date, time of day, reminders or overdue highlighting.
