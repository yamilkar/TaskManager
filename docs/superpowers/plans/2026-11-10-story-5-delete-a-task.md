# Story 5: Delete a task — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a signed-in user remove one of their own tasks after confirming, so it no longer appears in their list.

**Architecture:** Add one CQRS command (`DeleteTaskCommand` → `DeleteTaskHandler`) behind `DELETE /api/v1/tasks/{id}`. The handler makes a single conditional DynamoDB delete keyed by `(userId, taskId)`, so a missing task and another user's task both come back as `404`. In Angular, each `TaskList` row gets a Delete button that asks for confirmation inline (no dialog). On confirm, `TasksStore.remove` calls the API and drops the task from the list.

**Tech Stack:** .NET 10, ASP.NET Core controllers, AWSSDK.DynamoDBv2 (DynamoDB Local in tests), xUnit + Shouldly + NSubstitute. Angular standalone + signals, Angular Material, Vitest via `ng test`.

**Spec:** GitHub issue yamilkar/TaskManager#19 ("Story 5: Delete a task"). There is no separate design doc. The decisions below take its place.

**Branch:** `feature/story-5-delete-a-task`, cut from `main` **after** Story 4 (`feature/story-4-update-a-task`) merges. This plan builds on `UpdateAsync`, `TaskList.busy`/`pendingIds` and `TasksPage.onMove` from Story 4.

## Decisions

- **D29: hard delete, no soft delete or undo (YAGNI).** The item is removed from the Tasks table.
- **D30: one conditional write, no read first.** `DeleteItem` with `attribute_exists(#id)` on key `(userId, _id)`. If the condition fails, nothing matched: the task is gone or belongs to someone else. Either way the result is `TaskErrors.NotFound`.
- **D31: `204 No Content` on success.** `DELETE` of a missing task returns `404`, not `204`.
- **D32: the confirmation is inline in the row.** Clicking **Delete** swaps the row's actions for "Delete this task?" with **Delete** and **Keep** buttons. Only one row confirms at a time. No `MatDialog` and no `window.confirm`.
- **D33: the client treats `404` on delete as success.** If the task is already gone, it is removed from the list and the user sees "Task deleted".
- **D34: Delete is disabled where Move is disabled.** That is while a save is in flight, on the row open in the edit form, and while that row has a move or delete pending. So the form can never hold a deleted task.
- **D35: `ICommandHandler` needs a response type.** Add `SharedKernel.Unit` (an empty `readonly record struct`) for commands that return nothing.

## Global Constraints

- Clean Architecture: `Api → Application → Domain`, `Infrastructure → Application`. No `Amazon.*` or `Microsoft.AspNetCore.*` in Domain or Application. NetArchTest stays green.
- The command record and its sealed handler share one file: `src/Tasks/TaskManager.Tasks.Application/DeleteTask/DeleteTask.cs`. No mediator, no `*Service`.
- `userId` comes only from `CurrentUserId` (JWT `sub`), never from the route or a body.
- `ConditionalCheckFailedException` is caught in the repository and returned as `false`, never thrown.
- Tests are named `Method_condition_expected` (backend) and `'<Class>: <behavior>'` / `'<Class>.<method> <behavior>'` (web, matching the existing specs). Write each test red first.
- `dotnet build -warnaserror` is clean. Domain and Application line coverage stays ≥ 80%. `ng lint --max-warnings 0` passes, with zero console warnings.
- Conventional Commits, each ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- UI copy, exact: "Delete", "Delete this task?", "Keep", "Task deleted", "Couldn't delete the task: <reason>". Aria labels: "Delete <title>", "Confirm delete <title>".

## Review Focus

1. **Another user's task id**: `DELETE` returns `404` and that user's task is still stored. Pinned in Task 1 by `DeleteAsync_sameIdUnderOtherUser_returnsFalseAndKeepsOwnerTask`.
2. **Deleting twice or a stale tab** (the server gives `404`): the row disappears, and the user sees no error. Pinned in Task 4 by `TasksStore.remove treats 404 as already removed`.
3. **A failed delete** (5xx or network): the task stays in the list and a snackbar gives the reason. Pinned in Task 4 by `TasksStore.remove rethrows other failures and leaves the list unchanged`.
4. **Keep cancels**: nothing is emitted and the row's normal actions come back. Pinned in Task 5.
5. **Deleting the task open in the edit form** cannot happen: its Delete is disabled (D34). Pinned in Task 5.

---

### Task 1: Repository — `DeleteAsync`

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Application/Ports/ITaskRepository.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Infrastructure/Persistence/DynamoDbTaskRepository.cs`
- Test: `tests/TaskManager.Tasks.Infrastructure.Tests/DynamoDbTaskRepositoryTests.cs`

**Interfaces:**
- Produces: `Task<bool> ITaskRepository.DeleteAsync(Guid userId, Guid taskId, CancellationToken ct)`. Returns `false` when no item matched, and deletes nothing in that case.

- [ ] **Step 1: Write the failing tests** (use the existing `NewTask` helper)

```csharp
[Fact]
public async Task DeleteAsync_existingTask_removesItAndReturnsTrue()
{
    var task = NewTask(Guid.NewGuid());
    await _repo.AddAsync(task, default);

    (await _repo.DeleteAsync(task.UserId, task.Id, default)).ShouldBeTrue();

    (await _repo.GetAsync(task.UserId, task.Id, default)).ShouldBeNull();
}

[Fact]
public async Task DeleteAsync_sameIdUnderOtherUser_returnsFalseAndKeepsOwnerTask()
{
    var task = NewTask(Guid.NewGuid());
    await _repo.AddAsync(task, default);

    (await _repo.DeleteAsync(Guid.NewGuid(), task.Id, default)).ShouldBeFalse();

    (await _repo.GetAsync(task.UserId, task.Id, default)).ShouldNotBeNull();
}
```

- [ ] **Step 2: Run them and check that they fail**

Run: `dotnet test tests/TaskManager.Tasks.Infrastructure.Tests --filter "FullyQualifiedName~DeleteAsync"`
Expected: build error, because `DeleteAsync` does not exist yet.

- [ ] **Step 3: Implement `DeleteAsync`**

Add the method to the port with a one-line doc comment, mirroring `UpdateAsync`. In `DynamoDbTaskRepository`, send a `DeleteItemRequest` with `Key` = `userId`/`_id` (as in `GetAsync`), `ConditionExpression = "attribute_exists(#id)"` and `#id → _id`. Return `true`. Catch `ConditionalCheckFailedException` and return `false`.

- [ ] **Step 4: Run them and check that they pass** (same command; expected: PASS)

- [ ] **Step 5: Commit** — `feat(tasks): add conditional DeleteAsync to the task repository`

---

### Task 2: Application — `DeleteTaskHandler`

**Files:**
- Create: `src/BuildingBlocks/TaskManager.SharedKernel/Unit.cs`
- Create: `src/Tasks/TaskManager.Tasks.Application/DeleteTask/DeleteTask.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Application/DependencyInjection.cs`, `TasksMetrics.cs`
- Test: `tests/TaskManager.Tasks.Application.Tests/DeleteTaskHandlerTests.cs`

**Interfaces:**
- Consumes: `ITaskRepository.DeleteAsync` (Task 1).
- Produces:
  - `public readonly record struct Unit;` in namespace `TaskManager.SharedKernel`.
  - `public sealed record DeleteTaskCommand(Guid UserId, Guid TaskId);`
  - `public sealed class DeleteTaskHandler(ITaskRepository tasks, IAppMetrics metrics) : ICommandHandler<DeleteTaskCommand, Unit>`
  - `TasksMetrics.TaskDeleted = "TaskDeleted"`
  - A scoped DI registration of `ICommandHandler<DeleteTaskCommand, Unit>`.

- [ ] **Step 1: Write the failing tests**

```csharp
public class DeleteTaskHandlerTests
{
    private readonly ITaskRepository _repo = Substitute.For<ITaskRepository>();
    private readonly IAppMetrics _metrics = Substitute.For<IAppMetrics>();
    private readonly DeleteTaskCommand _command = new(Guid.NewGuid(), Guid.NewGuid());
    private DeleteTaskHandler Sut => new(_repo, _metrics);

    [Fact]
    public async Task HandleAsync_existingTask_deletesWithCallerIdAndCountsMetric()
    {
        _repo.DeleteAsync(_command.UserId, _command.TaskId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Sut.HandleAsync(_command, default);

        Assert.True(result.IsSuccess);
        _metrics.Received(1).Increment(TasksMetrics.TaskDeleted);
    }

    [Fact]
    public async Task HandleAsync_nothingDeleted_returnsNotFound()
    {
        var result = await Sut.HandleAsync(_command, default);

        Assert.Equal(TaskErrors.NotFound, result.Error);
        _metrics.DidNotReceive().Increment(Arg.Any<string>());
    }
}
```

- [ ] **Step 2: Run them and check that they fail**

Run: `dotnet test tests/TaskManager.Tasks.Application.Tests --filter "FullyQualifiedName~DeleteTaskHandler"`
Expected: build error, because `DeleteTaskCommand` does not exist yet.

- [ ] **Step 3: Implement the handler**

Add `Unit`, the command and handler, the metric constant and the DI line. The handler awaits `DeleteAsync`. If it returns false, return `TaskErrors.NotFound`. Otherwise increment the metric and return `default(Unit)`. Copy `UpdateTaskHandler`'s comment that the `(userId, taskId)` key makes another user's task "not found".

- [ ] **Step 4: Run them and check that they pass**

Run the same filter, then `dotnet test tests/TaskManager.ArchitectureTests`.
Expected: PASS. The handler is sealed and implements `ICommandHandler`.

- [ ] **Step 5: Commit** — `feat(tasks): add DeleteTask command handler`

---

### Task 3: API — `DELETE /api/v1/tasks/{id}`

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Api/Controllers/TasksController.cs`
- Modify: `tests/TaskManager.Tasks.Api.Tests/TasksApiFactory.cs` (add a `Delete` substitute and `Replace` it, like `Update`)
- Test: `tests/TaskManager.Tasks.Api.Tests/TasksEndpointsTests.cs`

**Interfaces:**
- Consumes: `ICommandHandler<DeleteTaskCommand, Unit>` (Task 2).
- Produces: `DELETE /api/v1/tasks/{id:guid}`, which returns `204` with no body, or a ProblemDetails `404`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Delete_with_token_passes_sub_and_route_id_and_returns_204()
{
    using var factory = new TasksApiFactory();
    var userId = Guid.NewGuid();
    var taskId = Guid.NewGuid();
    factory.Delete.HandleAsync(Arg.Any<DeleteTaskCommand>(), Arg.Any<CancellationToken>())
        .Returns(Result<Unit>.Success(default));

    var response = await ClientWith(factory, userId).DeleteAsync($"{Url}/{taskId}");

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await factory.Delete.Received(1).HandleAsync(
        Arg.Is<DeleteTaskCommand>(c => c.UserId == userId && c.TaskId == taskId), Arg.Any<CancellationToken>());
}

[Fact]
public async Task Delete_missing_task_returns_404_problem()
{
    using var factory = new TasksApiFactory(useRealHandlers: true); // Repository.DeleteAsync defaults to false

    var response = await ClientWith(factory, Guid.NewGuid()).DeleteAsync($"{Url}/{Guid.NewGuid()}");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
}
```

- [ ] **Step 2: Run them and check that they fail**

Run: `dotnet test tests/TaskManager.Tasks.Api.Tests --filter "FullyQualifiedName~Delete_"`
Expected: the build fails because `factory.Delete` does not exist. After adding it, the tests fail with `405`.

- [ ] **Step 3: Implement `Delete(Guid id, [FromServices] ICommandHandler<DeleteTaskCommand, Unit> handler, CancellationToken ct)`**

Use `[HttpDelete("{id:guid}")]`, then `result.IsSuccess ? NoContent() : Problem(result.Error!)`.

- [ ] **Step 4: Run them and check that they pass** (same command; expected: PASS). Then run `dotnet build -warnaserror` and expect a clean build.

- [ ] **Step 5: Commit** — `feat(tasks): expose DELETE /api/v1/tasks/{id}`

---

### Task 4: Web data — `TasksApi.delete` and `TasksStore.remove`

**Files:**
- Modify: `web/src/app/features/tasks/data/tasks-api.ts`, `tasks-store.ts`
- Test: `web/src/app/features/tasks/data/tasks-api.spec.ts`, `tasks-store.spec.ts`

**Interfaces:**
- Produces:
  - `TasksApi.delete(id: string): Observable<void>`, which sends `DELETE ${baseUrl}/api/v1/tasks/${id}`.
  - `TasksStore.remove(id: string): Promise<void>`, which drops the task from `tasks` on success or on a `404` (D33). Any other `AppError` is rethrown and the list is left unchanged.

- [ ] **Step 1: Write the failing tests**

In `tasks-api.spec.ts`, follow the `update` test:
`it('delete sends DELETE to /api/v1/tasks/{id}')`. Expect one request to `http://tasks/api/v1/tasks/7` with method `DELETE`.

In `tasks-store.spec.ts` (add `delete: vi.fn()` to the API mock):

```ts
it('TasksStore.remove drops the task from the list', async () => {
  api.list.mockReturnValue(of([mk('a'), mk('b')]));
  api.delete.mockReturnValue(of(undefined));
  const store = setup();
  await store.load();

  await store.remove('a');

  expect(api.delete).toHaveBeenCalledWith('a');
  expect(store.tasks().map((t) => t.id)).toEqual(['b']);
});

it('TasksStore.remove treats 404 as already removed', async () => {
  api.list.mockReturnValue(of([mk('a')]));
  api.delete.mockReturnValue(throwError(() => ({ status: 404, message: 'Task not found.', fieldErrors: {} })));
  const store = setup();
  await store.load();

  await store.remove('a');

  expect(store.tasks()).toEqual([]);
});

it('TasksStore.remove rethrows other failures and leaves the list unchanged', async () => {
  const err: AppError = { status: 500, message: 'boom', fieldErrors: {} };
  api.list.mockReturnValue(of([mk('a')]));
  api.delete.mockReturnValue(throwError(() => err));
  const store = setup();
  await store.load();
  const before = store.tasks();

  await expect(store.remove('a')).rejects.toEqual(err);
  expect(store.tasks()).toBe(before);
});
```

- [ ] **Step 2: Run them and check that they fail**

Run: `cd web && npx ng test --watch=false`
Expected: the new tests fail because `delete` and `remove` are not defined.

- [ ] **Step 3: Implement both methods.** `remove` wraps `firstValueFrom(this.api.delete(id))` in a try/catch that rethrows unless `(e as AppError).status === 404`, then filters the task out of `tasksState`.

- [ ] **Step 4: Run them and check that they pass** (same command; expected: PASS)

- [ ] **Step 5: Commit** — `feat(web): add task delete to TasksApi and TasksStore`

---

### Task 5: Web UI — confirm in the row, delete from the page

**Files:**
- Modify: `web/src/app/features/tasks/task-list/task-list.ts`, `task-list.html`
- Modify: `web/src/app/features/tasks/tasks-page/tasks-page.ts`, `tasks-page.html`
- Test: `web/src/app/features/tasks/task-list/task-list.spec.ts`, `tasks-page/tasks-page.spec.ts`

**Interfaces:**
- Consumes: `TasksStore.remove` (Task 4).
- Produces:
  - `TaskList.delete = output<Task>()`, emitted only on confirm.
  - Private `TaskList.confirmingId = signal<string | null>(null)`.
  - `TasksPage.onDelete(task: Task): Promise<void>`.
  - Rename `TasksPage.pendingMoves` to `pending`, because it now holds ids with a move **or** delete in flight. It is still bound to `[pendingIds]`.

- [ ] **Step 1: Write the failing tests**

`task-list.spec.ts`:

```ts
it('TaskList: Delete asks to confirm and emits delete only on confirm', () => {
  const fixture = setupFixture([mk({ id: '1', title: 'A' })]);
  const el = fixture.nativeElement as HTMLElement;
  const emitted: Task[] = [];
  fixture.componentInstance.delete.subscribe((t) => emitted.push(t));

  el.querySelector<HTMLButtonElement>('[aria-label="Delete A"]')!.click();
  fixture.detectChanges();
  expect(el.textContent).toContain('Delete this task?');
  expect(emitted).toEqual([]);

  el.querySelector<HTMLButtonElement>('[aria-label="Confirm delete A"]')!.click();
  expect(emitted.map((t) => t.id)).toEqual(['1']);
});

it('TaskList: Keep cancels the confirmation without emitting', () => {
  // click "Delete A", then the "Keep" button. Expect no emission, the
  // "Delete this task?" text gone, and "Delete A" visible again.
});

it('TaskList: the row being edited has Delete disabled', () => {
  // setupFixture([mk({ id: '1', title: 'A' })], '1') → the "Delete A" button is disabled.
});
```

`tasks-page.spec.ts` (end-to-end through `HttpTestingController`):

```ts
it('TasksPage: confirming delete removes the task and shows "Task deleted"', async () => {
  const { fixture, el } = setup();
  ctrl.expectOne(URL).flush([mk('1', 'Gone'), mk('2', 'Stays')]);
  await settle(fixture);

  el.querySelector<HTMLButtonElement>('[aria-label="Delete Gone"]')!.click();
  fixture.detectChanges();
  el.querySelector<HTMLButtonElement>('[aria-label="Confirm delete Gone"]')!.click();
  const req = ctrl.expectOne(`${URL}/1`);
  expect(req.request.method).toBe('DELETE');
  req.flush(null, { status: 204, statusText: 'No Content' });
  await settle(fixture);

  expect(el.textContent).not.toContain('Gone');
  expect(el.textContent).toContain('Stays');
  expect(document.body.textContent).toContain('Task deleted');
});
```

- [ ] **Step 2: Run them and check that they fail** (`cd web && npx ng test --watch=false`; expected: the new tests fail)

- [ ] **Step 3: Implement**

- **`task-list.html`**: inside `.actions`, use `@if (confirmingId() === task.id)` to render the text "Delete this task?", a **Delete** button (`aria-label="Confirm delete <title>"`, which sets `confirmingId` to null and emits `delete`) and a **Keep** button (sets `confirmingId` to null). In the `@else` branch, render the existing Edit and Move buttons plus a **Delete** button (`aria-label="Delete <title>"`, which sets `confirmingId` to `task.id`). The Delete button's `[disabled]` uses exactly the same expression as Move (D34).
- **`tasks-page`**: bind `(delete)="onDelete($event)"`. `onDelete` copies `onMove`'s structure: add the id to `pending`, then `await store.remove(task.id)`, then show the snackbar "Task deleted" (3000 ms). On error, show "Couldn't delete the task: <err.message>" (5000 ms). In `finally`, remove the id from `pending`.

- [ ] **Step 4: Run them and check that they pass**

Run: `cd web && npx ng test --watch=false && npx ng lint --max-warnings 0`
Expected: all tests pass and lint is clean.

- [ ] **Step 5: Commit** — `feat(web): delete a task with inline confirmation`

---

### Task 6: Status and final check

**Files:**
- Modify: `README.md` (the "Project status" section: Story 5 in progress, and done when the PR merges)

- [ ] **Step 1: Update the README "Project status" line for Story 5.**
- [ ] **Step 2: Run the full gate**

Run `dotnet build -warnaserror`, then `dotnet test` (with coverage, as CI does), then `cd web && npx ng test --watch=false && npx ng lint --max-warnings 0`.
Expected: everything is green and Domain and Application coverage is ≥ 80%.

- [ ] **Step 3: Manually check in the browser.** Delete a task, then reload and confirm it is still gone. Confirm Keep cancels. Confirm the console has no warnings.
- [ ] **Step 4: Commit** — `docs: mark Story 5 in progress`
