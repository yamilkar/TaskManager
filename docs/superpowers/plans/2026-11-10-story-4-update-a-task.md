# Story 4: Update a task — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a signed-in user edit any field of their own task and move it between To do, In progress and Done, with the same rules and messages as create.

**Architecture:** Add one CQRS command (`UpdateTaskCommand` → `UpdateTaskHandler`) behind `PUT /api/v1/tasks/{id}`. The handler reads the task, calls the immutable domain method `TaskItem.Update`, and saves it with a conditional DynamoDB put that never upserts. The Angular `TaskForm` gets an edit mode, and the `TaskList` rows get Edit and Move actions wired through `TasksStore.update`.

**Tech Stack:** .NET 10, ASP.NET Core controllers, AWSSDK.DynamoDBv2 (DynamoDB Local in tests), xUnit + Shouldly + NSubstitute; Angular 22 standalone + signals, Angular Material (`MatMenu` is new here), Vitest via `ng test`.

**Spec:** `docs/superpowers/specs/2026-11-10-story-4-update-a-task-design.md` (decisions D21–D28). Field rules: `docs/superpowers/specs/2026-11-10-story-1-foundation-design.md` §4.3 and D9.

**Branch:** `feature/story-4-update-a-task`, cut from `main` **after** Story 2 (#32) merges. Story 3 (`feature/story-3-describe-my-task`) also edits `TaskItem.Create`'s due-date message and `task-form.*`. Whichever lands second rebases. Because of D22, Story 3's message reaches update automatically, so tests here assert `TaskItem`'s message through the shared error key `dueDate` and the text in use on `main` at implementation time.

## Global Constraints

- Clean Architecture: `Api → Application → Domain`, `Infrastructure → Application`. No `Amazon.*` or `Microsoft.AspNetCore.*` in Domain or Application. NetArchTest stays green.
- One command record and one sealed handler in one file, `src/Tasks/TaskManager.Tasks.Application/UpdateTask/UpdateTask.cs`. No mediator, no `*Service`, no EF or Dapper.
- `userId` always comes from the JWT `sub` claim (`CurrentUserId`), never from the body or the route.
- Business failures return `Result<T>`. `ConditionalCheckFailedException` is caught in the repository and returned as `false`, never thrown to the handler.
- Tests are named `Method_condition_expected`. Red first for all new behavior.
- `dotnet build -warnaserror` is clean. Domain and Application line coverage stays ≥ 80%.
- Angular: standalone, `OnPush`, `inject()`, `input()`/`output()`, `@if`/`@for`, no `.component` file suffix, `ng lint --max-warnings 0`, zero browser console warnings and errors.
- Conventional Commits, each ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. No symbolic links.
- UI copy, exact: "Edit task", "Add a task", "Save changes", "Add task", "Cancel", "Edit", "Move", "Task updated", "Moved to <label>", "Couldn't move the task: <reason>". Status labels: "To do", "In progress", "Done".

## Review Focus

1. **An overdue task can still be edited or moved** (D24): changing only the status or title of a task whose stored due date is in the past succeeds in the domain, the API, the form and the Move menu.
2. **The task is gone or belongs to someone else**: `PUT` returns `404`, writes nothing, and never recreates an item deleted between the read and the write.
3. **A PUT without `status`** returns `400` with the status error. It never silently resets the task to To do (D23).
4. **A failed save stays in edit mode**: the typed values stay, server field errors show under the fields, and the list still shows the old values.
5. **Cancel, or Edit on another task, drops unsaved edits**: the form shows the newly chosen task (or the empty "Add a task" form), with no leftover server errors.

---

### Task 1: Domain — `TaskItem.Update`

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Domain/TaskItem.cs`
- Test: `tests/TaskManager.Tasks.Domain.Tests/TaskItemTests.cs`

**Interfaces:**
- Produces: `public Result<TaskItem> Update(string? title, string? description, string? status, DateOnly? dueDate, DateTimeOffset now)`. It is an instance method returning a new `TaskItem`: same `Id`, `UserId`, `CreatedAt`, and `UpdatedAt = now`. Error shape is identical to `Create`: `Error.Validation("validation_failed", "One or more validation errors occurred.", fieldErrors)`.

- [ ] **Step 1: Write the failing tests** in `TaskItemTests.cs`. Use a helper `Existing(DateOnly? due = null, TaskItemStatus status = ToDo)` built with `TaskItem.Rehydrate(...)`, with `createdAt = 2026-11-01T09:00Z`; `now = 2026-11-10T12:00Z`.
  - `Update_validFields_returnsNewItemWithSameIdOwnerCreatedAtAndUpdatedAtNow`: title `"  New  "` → `"New"`, description `"d"`, status `"Done"` → `TaskItemStatus.Done`, due `2026-11-20`. Asserts `Id`, `UserId` and `CreatedAt` are unchanged, `UpdatedAt == now`, and the original instance is unchanged.
  - `Update_blankTitle_returnsTitleRequiredError` (theory: `null`, `""`, `"   "`) → `FieldErrors["title"] == ["Title is required."]`.
  - `Update_201CharTitle_returnsMaxLengthError`, and `Update_2001CharDescription_returnsMaxLengthError`.
  - `Update_blankDescription_clearsDescription` → `Description == null`.
  - `Update_nullDueDate_clearsDueDate` → `DueDate == null`.
  - `Update_missingStatus_returnsStatusError` (D23) → `FieldErrors["status"] == [TaskItemStatuses.InvalidMessage]`.
  - `Update_unknownStatus_returnsStatusError` with `"Blocked"`.
  - `Update_unchangedPastDueDate_isAccepted` (D24): existing due `2026-11-01`, update with due `2026-11-01` and status `"Done"` → success.
  - `Update_newPastDueDate_returnsDueDateError`: existing due `null`, new due `2026-11-08` → `FieldErrors` has key `"dueDate"` with the same message `Create` returns for that date (assert `ShouldBe(TaskItem.Create(...same date...).Error!.FieldErrors!["dueDate"])`, so Story 3's wording needs no edit here).
  - `Update_changedPastDueDateToOtherPastDate_returnsDueDateError`: existing `2026-11-01` → new `2026-11-02`.
  - `Update_badStatusAndBlankTitle_reportsBothInOneError`.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/TaskManager.Tasks.Domain.Tests --filter "FullyQualifiedName~TaskItemTests.Update"`
Expected: build error, `'TaskItem' does not contain a definition for 'Update'`.

- [ ] **Step 3: Implement `Update` in `TaskItem.cs`.** Extract the field checks from `Create` into `private static Dictionary<string, string[]> Validate(string? trimmedTitle, string? trimmedDescription, string? status, bool statusRequired, DateOnly? dueDate, DateOnly? unchangedDueDate, DateTimeOffset now, out TaskItemStatus parsedStatus)`. Skip the past-date check when `dueDate == unchangedDueDate`. When `statusRequired` is set and `status is null`, add the `status` error. `Create` passes `statusRequired: false` and `unchangedDueDate: null`. Keep the D9 comment.

- [ ] **Step 4: Run the domain test project** (all of it, to prove `Create` is unchanged)

Run: `dotnet test tests/TaskManager.Tasks.Domain.Tests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Tasks/TaskManager.Tasks.Domain/TaskItem.cs tests/TaskManager.Tasks.Domain.Tests/TaskItemTests.cs
git commit -m "feat(tasks): add TaskItem.Update with shared field rules"
```

---

### Task 2: Repository — conditional `UpdateAsync`

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Application/Ports/ITaskRepository.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Infrastructure/Persistence/DynamoDbTaskRepository.cs`
- Test: `tests/TaskManager.Tasks.Infrastructure.Tests/DynamoDbTaskRepositoryTests.cs`

**Interfaces:**
- Produces: `Task<bool> UpdateAsync(TaskItem task, CancellationToken ct);` on `ITaskRepository`. It returns `true` when the item `(task.UserId, task.Id)` existed and was replaced, and `false` when it did not exist (nothing written).

- [ ] **Step 1: Write the failing tests** (DynamoDB Local, existing `NewTask` helper):
  - `UpdateAsync_existingTask_replacesFieldsAndReturnsTrue`: add a task, then update it via `Rehydrate` with a new title, `Done` and due `2026-12-01`. Assert the result is `true`, and that `GetAsync` returns the new title, status and due date with the original `CreatedAt`.
  - `UpdateAsync_clearedDescriptionAndDueDate_removesThemFromItem`: add with description `"d"` and a due date, update with both `null`. `GetAsync` shows `Description` and `DueDate` as `null`.
  - `UpdateAsync_missingTask_returnsFalseAndCreatesNothing`: update a task never added → `false`, and `GetAsync` returns `null`.
  - `UpdateAsync_sameIdUnderOtherUser_returnsFalseAndLeavesOwnerTaskUnchanged`: add a task for user A, then update a `Rehydrate` with the same `Id` but user B → `false`. A's task is unchanged and B has no item.

- [ ] **Step 2: Run to fail**

Run: `dotnet test tests/TaskManager.Tasks.Infrastructure.Tests --filter "FullyQualifiedName~UpdateAsync"`
Expected: build error, `UpdateAsync` not defined.

- [ ] **Step 3: Add the port method, then implement it** as a `PutItemRequest` with `Item = TaskItemMapper.ToItem(task)` and `ConditionExpression = "attribute_exists(#id)"` (`#id` → `_id`). Catch `ConditionalCheckFailedException` → `false`. The Api test factory's substitute `ITaskRepository` needs no change.

- [ ] **Step 4: Run the infrastructure tests**

Run: `dotnet test tests/TaskManager.Tasks.Infrastructure.Tests`
Expected: all pass (DynamoDB Local is started by `DynamoDbLocalFixture`).

- [ ] **Step 5: Commit**

```bash
git add src/Tasks/TaskManager.Tasks.Application/Ports/ITaskRepository.cs src/Tasks/TaskManager.Tasks.Infrastructure/Persistence/DynamoDbTaskRepository.cs tests/TaskManager.Tasks.Infrastructure.Tests/DynamoDbTaskRepositoryTests.cs
git commit -m "feat(tasks): add conditional UpdateAsync to the task repository"
```

---

### Task 3: Application — `UpdateTaskHandler`

**Files:**
- Create: `src/Tasks/TaskManager.Tasks.Application/UpdateTask/UpdateTask.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Application/DependencyInjection.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Application/TasksMetrics.cs`
- Test: `tests/TaskManager.Tasks.Application.Tests/UpdateTaskHandlerTests.cs` (new)

**Interfaces:**
- Consumes: `TaskItem.Update` (Task 1), `ITaskRepository.GetAsync` / `UpdateAsync` (Task 2), `TaskErrors.NotFound`, `TaskMapping.ToResponse`.
- Produces:
  - `public sealed record UpdateTaskCommand(Guid UserId, Guid TaskId, string? Title, string? Description, string? Status, DateOnly? DueDate);`
  - `public sealed class UpdateTaskHandler(ITaskRepository tasks, IClock clock, IAppMetrics metrics) : ICommandHandler<UpdateTaskCommand, TaskResponse>`
  - `TasksMetrics.TaskUpdated = "TaskUpdated"`
  - DI: `services.AddScoped<ICommandHandler<UpdateTaskCommand, TaskResponse>, UpdateTaskHandler>()`

- [ ] **Step 1: Write the failing tests** (pattern of `CreateTaskHandlerTests`: substitute repo, `FixedClock(Now)`, substitute metrics):
  - `HandleAsync_validCommand_savesUpdatedTaskAndReturnsResponse`: `GetAsync(userId, taskId)` returns an existing task and `UpdateAsync` returns `true`. Asserts the response title and status, `UpdatedAt == Now`, and the original `CreatedAt`. `UpdateAsync` received a `TaskItem` with the same `Id` and `UserId`, and `metrics.Increment("TaskUpdated")` was called once.
  - `HandleAsync_taskNotFound_returnsNotFoundAndDoesNotWrite`: `GetAsync` returns `null` → `result.Error == TaskErrors.NotFound`, `UpdateAsync` was not received, and no metric was recorded.
  - `HandleAsync_readsWithCallerId_soOtherUsersTaskIsNotFound`: asserts `GetAsync` was received with `command.UserId` and `command.TaskId` exactly.
  - `HandleAsync_invalidFields_returnsValidationErrorAndDoesNotWrite`: blank title → `ErrorType.Validation`, with a `title` key; `UpdateAsync` was not received.
  - `HandleAsync_taskDeletedBeforeWrite_returnsNotFound`: `GetAsync` returns a task and `UpdateAsync` returns `false` → `TaskErrors.NotFound`, and no metric.

- [ ] **Step 2: Run to fail**

Run: `dotnet test tests/TaskManager.Tasks.Application.Tests --filter "FullyQualifiedName~UpdateTaskHandlerTests"`
Expected: build error, `UpdateTaskCommand` not found.

- [ ] **Step 3: Implement** the record, the handler (get → `NotFound` if null → `Update` → return the error on failure → `UpdateAsync` → `NotFound` if `false` → metric → `ToResponse()`), the metric constant and the DI line. Add an XML `<param name="UserId">` comment like `CreateTaskCommand`'s.

- [ ] **Step 4: Run the Application and Architecture tests**

Run: `dotnet test tests/TaskManager.Tasks.Application.Tests` then `dotnet test tests/TaskManager.ArchitectureTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Tasks/TaskManager.Tasks.Application tests/TaskManager.Tasks.Application.Tests/UpdateTaskHandlerTests.cs
git commit -m "feat(tasks): add UpdateTask command and handler"
```

---

### Task 4: Api — `PUT /api/v1/tasks/{id}`

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Api/Controllers/TasksController.cs`
- Modify: `tests/TaskManager.Tasks.Api.Tests/TasksApiFactory.cs`
- Test: `tests/TaskManager.Tasks.Api.Tests/TasksEndpointsTests.cs`

**Interfaces:**
- Consumes: `UpdateTaskCommand`, `ICommandHandler<UpdateTaskCommand, TaskResponse>` (Task 3).
- Produces: `public sealed record UpdateTaskRequest(string? Title, string? Description, string? Status, DateOnly? DueDate);` and the action `[HttpPut("{id:guid}")] Update(Guid id, [FromBody] UpdateTaskRequest request, [FromServices] ICommandHandler<UpdateTaskCommand, TaskResponse> handler, CancellationToken ct)` → `Ok(result.Value)` or `Problem(result.Error!)`. Factory: a `public ICommandHandler<UpdateTaskCommand, TaskResponse> Update { get; }` substitute, replaced when `!useRealHandlers`.

- [ ] **Step 1: Write the failing tests** (helper `Put(client, id, json)` = `client.PutAsync($"{Url}/{id}", Json(json))`).

  Substituted handler:
  - `Update_without_token_returns_401`.
  - `Update_with_token_passes_sub_route_id_and_body_to_handler`: the handler returns `Sample(taskId,"New")` → `200`, body `title == "New"`, received `c.UserId == userId && c.TaskId == taskId && c.Title == "New" && c.Status == "Done" && c.DueDate == new DateOnly(2026,12,1)`.
  - `Update_ignores_userId_id_and_createdAt_in_body`: the body has `userId`, `id` (another GUID) and `createdAt`. The handler receives the token's `sub` and the route id.
  - `Update_with_invalid_due_date_format_returns_400`: `"dueDate":"10/11/2026"`, handler not received.
  - `Update_with_non_guid_id_returns_404`: `PUT /api/v1/tasks/abc`.

  Real handlers (`new TasksApiFactory(useRealHandlers: true)`, `factory.Repository` set up):
  - `Update_valid_body_returns_200_with_changes`: `GetAsync(userId, taskId)` returns an existing task and `UpdateAsync` returns `true` → `200`, with the new title, description and dueDate.
  - `Update_status_only_change_returns_200`: an existing `ToDo` task, a body with the same fields and `"status":"InProgress"` → `200`, `status == "InProgress"`.
  - `Update_unknown_task_returns_404_problem`: `GetAsync` returns `null` → `404`, `application/problem+json`, `code == "tasks.not_found"`; `UpdateAsync` not received.
  - `Update_blank_title_returns_400_with_title_error` → `errors.title[0] == "Title is required."`.
  - `Update_without_status_returns_400_with_status_error` → `errors.status[0] == "Status must be one of: ToDo, InProgress, Done."`.
  - `Update_unchanged_past_due_date_returns_200`: existing due date = today UTC − 10 days, the body sends the same date with `"status":"Done"`.
  - `Update_new_past_due_date_returns_400`: existing due date `null`, body due date = today UTC − 10 days → the `errors.dueDate` key is present.

- [ ] **Step 2: Run to fail**

Run: `dotnet test tests/TaskManager.Tasks.Api.Tests --filter "FullyQualifiedName~TasksEndpointsTests.Update"`
Expected: build error (`factory.Update` missing), or `405` responses once that compiles.

- [ ] **Step 3: Implement** the request record, the controller action and the factory property, plus its `Replace`.

- [ ] **Step 4: Run all backend tests and the warnings build**

Run: `dotnet build TaskManager.slnx -warnaserror` then `dotnet test TaskManager.slnx`
Expected: build succeeds with 0 warnings; all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Tasks/TaskManager.Tasks.Api/Controllers/TasksController.cs tests/TaskManager.Tasks.Api.Tests
git commit -m "feat(tasks-api): add PUT /api/v1/tasks/{id}"
```

---

### Task 5: Web data layer — `TasksApi.update`, `TasksStore.update`, `STATUS_LABELS`

**Files:**
- Modify: `web/src/app/features/tasks/data/task.model.ts`
- Modify: `web/src/app/features/tasks/data/tasks-api.ts`, `tasks-api.spec.ts`
- Modify: `web/src/app/features/tasks/data/tasks-store.ts`, `tasks-store.spec.ts`
- Modify: `web/src/app/shared/ui/status-badge/status-badge.ts` (import `STATUS_LABELS` instead of its local `LABELS`)

**Interfaces:**
- Produces:
  - `export interface UpdateTaskRequest { title: string; description: string | null; status: TaskStatus; dueDate: string | null; }`
  - `export const STATUS_LABELS: Record<TaskStatus, string>` = `{ ToDo: 'To do', InProgress: 'In progress', Done: 'Done' }`
  - `export const TASK_STATUSES: readonly TaskStatus[] = ['ToDo', 'InProgress', 'Done']`
  - `TasksApi.update(id: string, req: UpdateTaskRequest): Observable<Task>` → `PUT ${tasksApiUrl}/api/v1/tasks/${id}`
  - `TasksStore.update(id: string, req: UpdateTaskRequest): Promise<Task>` replaces the task with that id **in place** (same index) and rethrows on failure with the list unchanged.

- [ ] **Step 1: Write the failing tests**
  - `tasks-api.spec.ts` › `update PUTs the request body to /api/v1/tasks/{id}`: `expectOne({ method: 'PUT', url: 'http://tasks/api/v1/tasks/t1' })`, and `request.body` equals the request.
  - `tasks-store.spec.ts` › `TasksStore.update replaces the task in place`: load `[a, b, c]`, update `b` → `tasks()` is `[a, b', c]`.
  - `TasksStore.update rethrows failures and leaves the list unchanged`: a `400` flush → the promise rejects, and `tasks()` is still `[a, b, c]` (same references).
  - The existing `StatusBadge` specs (if any) still pass, with labels unchanged.

- [ ] **Step 2: Run to fail**

Run: `cd web && npx ng test --watch=false --include src/app/features/tasks/data`
Expected: FAIL, with `update` not a function.

- [ ] **Step 3: Implement** the model exports, the `update` methods, and the `status-badge` import.

- [ ] **Step 4: Run to pass**

Run: `cd web && npx ng test --watch=false`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/features/tasks/data web/src/app/shared/ui/status-badge
git commit -m "feat(web): add task update to the tasks API and store"
```

---

### Task 6: `TaskForm` edit mode

**Files:**
- Modify: `web/src/app/features/tasks/task-form/task-form.ts`, `task-form.html`, `task-form.scss` (button row spacing only)
- Test: `web/src/app/features/tasks/task-form/task-form.spec.ts`

**Interfaces:**
- Consumes: `Task`, `UpdateTaskRequest` (Task 5).
- Produces (on `TaskForm`):
  - `readonly task = input<Task | null>(null);`: when non-null, the form is in edit mode.
  - `readonly submitted = output<UpdateTaskRequest>();`: the type changes from `CreateTaskRequest`; the payload shape is the same as today. `UpdateTaskRequest` is assignable to `CreateTaskRequest`, so the create path is unchanged.
  - `readonly cancelled = output<void>();`
  - `reset()` keeps its meaning (an empty add form).

- [ ] **Step 1: Write the failing tests** (set the input with `fixture.componentRef.setInput('task', t)`, then `detectChanges()`):
  - `TaskForm: edit mode prefills fields and shows Edit task and Save changes`: title, description, status select value and due date input value equal the task's. The heading is "Edit task" and the submit text is "Save changes". A null description or due date prefills `''`.
  - `TaskForm: edit mode focuses the title input`: `document.activeElement === input[name=title]`.
  - `TaskForm: edit mode emits all fields on save`: change the title → emitted `{ title, description, status, dueDate }` with the task's other values.
  - `TaskForm: edit mode accepts the task's unchanged past due date` (D24): a task with due `2000-01-01`, submit → emitted, and no "cannot be in the past" text.
  - `TaskForm: edit mode rejects a newly picked past date`: the same task, change the due date to `2000-01-02` → not emitted, past-date message shown.
  - `TaskForm: edit mode shows the same validation messages`: clear the title → "Title is required."; 201 chars → "Title must be at most 200 characters.".
  - `TaskForm: Cancel emits cancelled and is hidden in add mode`: no `button[type=button]` with text "Cancel" when `task` is null; in edit mode, a click emits once.
  - `TaskForm: switching task replaces values and clears server errors`: set task A, set `serverErrors` `{ title: ['x'] }`, set task B → the fields show B, and the text `x` is gone.
  - `TaskForm: setting task to null returns to an empty Add a task form`.

- [ ] **Step 2: Run to fail**

Run: `cd web && npx ng test --watch=false --include src/app/features/tasks/task-form`
Expected: FAIL (the `task` input is unknown).

- [ ] **Step 3: Implement.** Add an `effect` on `task()`. Non-null: `form.reset({...task values, '' for nulls})`, then focus the title via `viewChild<ElementRef<HTMLInputElement>>('titleInput')` inside `afterNextRender`. Null: `reset()`. The `form.reset` also clears server-error state. The due-date validator becomes a closure that reads `this.task()?.dueDate` and passes when `value === thatDate`. The template uses `task() ? 'Edit task' : 'Add a task'` and `task() ? 'Save changes' : 'Add task'`, and adds `@if (task()) { <button matButton type="button" (click)="cancelled.emit()">Cancel</button> }`.

- [ ] **Step 4: Run to pass**

Run: `cd web && npx ng test --watch=false --include src/app/features/tasks/task-form`
Expected: all pass, the old add-mode tests included.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/features/tasks/task-form
git commit -m "feat(web): add edit mode to the task form"
```

---

### Task 7: `TaskList` Edit and Move actions

**Files:**
- Modify: `web/src/app/features/tasks/task-list/task-list.ts`, `task-list.html`, `task-list.scss`
- Test: `web/src/app/features/tasks/task-list/task-list.spec.ts`

**Interfaces:**
- Consumes: `STATUS_LABELS`, `TASK_STATUSES`, `TaskStatus` (Task 5); `MatButton`, `MatIconButton` (if an icon is used), `MatMenu`, `MatMenuItem`, `MatMenuTrigger` from `@angular/material/menu`.
- Produces:
  - `readonly editingId = input<string | null>(null);`
  - `readonly edit = output<Task>();`
  - `readonly move = output<{ task: Task; status: TaskStatus }>();`

- [ ] **Step 1: Write the failing tests**
  - `TaskList: each row has an Edit button labelled with the task title`: `button[aria-label="Edit Buy milk"]`; a click emits `edit` with that task.
  - `TaskList: Move menu lists only the other two statuses`: click `button[aria-label="Move Buy milk"]` on a `ToDo` task. The overlay (`document.querySelectorAll('[role=menuitem]')`) shows exactly "In progress" and "Done".
  - `TaskList: choosing a status emits move`: click "Done" → emitted `{ task, status: 'Done' }`.
  - `TaskList: the row being edited has Move disabled`: `editingId` = the task's id → its Move button is `disabled`; other rows are enabled.
  - The existing render and empty-state tests still pass.

- [ ] **Step 2: Run to fail**

Run: `cd web && npx ng test --watch=false --include src/app/features/tasks/task-list`
Expected: FAIL (no Edit button).

- [ ] **Step 3: Implement** a `.actions` area in each row's `.head`. It holds the Edit text button (`matButton`, text "Edit") and the Move trigger (`[matMenuTriggerFor]`, text "Move"), with one `<mat-menu>` per row. The `@for` runs over `TASK_STATUSES`, skipping `task.status`. The labels come from `STATUS_LABELS`. Buttons wrap under the title at narrow widths, because Story 9 is mobile (`flex-wrap: wrap`).

- [ ] **Step 4: Run to pass, then lint**

Run: `cd web && npx ng test --watch=false --include src/app/features/tasks/task-list` then `npx ng lint --max-warnings 0`
Expected: pass; lint clean.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/features/tasks/task-list
git commit -m "feat(web): add Edit and Move actions to task rows"
```

---

### Task 8: `TasksPage` wiring, README and acceptance check

**Files:**
- Modify: `web/src/app/features/tasks/tasks-page/tasks-page.ts`, `tasks-page.html`
- Test: `web/src/app/features/tasks/tasks-page/tasks-page.spec.ts`
- Modify: `README.md` (capability table: "Update a task" → Done; the Project status row for Story 4; "Last updated")

**Interfaces:**
- Consumes: `TasksStore.update` (Task 5); `TaskForm.task` / `cancelled` (Task 6); `TaskList.editingId` / `edit` / `move` (Task 7); `STATUS_LABELS`.
- Produces: `protected readonly editing = signal<Task | null>(null)`; handlers `onEdit(task: Task)`, `onCancelEdit()`, `onMove(e: { task: Task; status: TaskStatus })`. `onSubmitted(req: UpdateTaskRequest)` branches on `editing()`. Field-error and message mapping is shared with create in one private method.

- [ ] **Step 1: Write the failing tests** (the existing `HttpTestingController` setup):
  - `TasksPage: edit saves changes in place`: load `[a, b]`, click Edit on `b`. The form heading is "Edit task". Change the title and click "Save changes" → `expectOne({ method: 'PUT', url: URL + '/b' })` body `{ title: 'New', description: null, status: 'ToDo', dueDate: null }`. Flush the updated `b` → the list order is `a, b'`, the snackbar shows "Task updated", and the heading is back to "Add a task" with an empty title.
  - `TasksPage: failed save stays in edit mode with field errors`: the PUT flushes `400` `{ errors: { title: ['Title is required.'] } }` → the heading is still "Edit task", the typed value is kept, the error is shown, and the list still shows the old title.
  - `TasksPage: cancel discards edits`: Edit `b`, type, click Cancel → "Add a task" with an empty form, no PUT (`ctrl.verify()`), and the list is unchanged.
  - `TasksPage: move menu changes status`: Move on `a` → "In progress" → PUT body equals `a`'s fields with `status: 'InProgress'`. Flush → the badge shows "In progress" and the snackbar shows "Moved to In progress".
  - `TasksPage: move of an overdue task sends its unchanged due date` (D24): `a.dueDate = '2000-01-01'`, move to Done → the PUT body has `dueDate: '2000-01-01'`.
  - `TasksPage: failed move shows the reason and keeps the list`: flush `404` `{ detail: 'Task not found.' }` → the snackbar shows "Couldn't move the task: Task not found." and the badge is still "To do".
  - `TasksPage: create still works while not editing`: the existing create test stays green.

- [ ] **Step 2: Run to fail**

Run: `cd web && npx ng test --watch=false --include src/app/features/tasks/tasks-page`
Expected: FAIL (no Edit wiring).

- [ ] **Step 3: Implement** the wiring in `tasks-page.ts` and `.html`: `[task]="editing()"`, `(cancelled)="onCancelEdit()"`, `[editingId]="editing()?.id ?? null"`, `(edit)="onEdit($event)"`, `(move)="onMove($event)"`. `onEdit` and `onCancelEdit` clear `serverErrors` and `formError`. A move failure message uses the first field error, if any, else `err.message`.

- [ ] **Step 4: Run all web checks**

Run: `cd web && npx ng test --watch=false` then `npx ng lint --max-warnings 0` then `npx ng build`
Expected: all pass, lint is clean, and the build succeeds with no warnings.

- [ ] **Step 5: Manual acceptance on the local stack** (README "Run locally", demo account). In the browser pane, run the spec §6 manual column: edit all fields; Move To do → In progress → Done; clear the title and save → the message shows; move an overdue seeded task. Check the console: zero warnings or errors on `/tasks`. Take screenshots for the PR.

- [ ] **Step 6: Update the README** capability table and **Project status** (Story 4 in review, PR link once opened), and bump "Last updated".

- [ ] **Step 7: Commit**

```bash
git add web/src/app/features/tasks/tasks-page README.md
git commit -m "feat(web): edit and move tasks from the task list"
```
