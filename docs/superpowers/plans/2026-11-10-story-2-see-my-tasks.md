# Story 2: See My Tasks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Page the task list newest-first with an opaque cursor (API and DynamoDB), add a "Load more" button to the Angular list, and record acceptance evidence for issue #16.

**Architecture:** A local secondary index `ByCreatedAt` (`userId` + `createdAtId`) lets one consistent DynamoDB `Query` return a page. The Application port takes `limit` and `cursor` and returns items plus `nextCursor` as plain strings; Infrastructure owns the cursor encoding. The existing CQRS query handler validates `limit` and maps a bad cursor to a `400` field error. The Angular signal store keeps the cursor and appends pages.

**Tech Stack:** .NET 10, ASP.NET Core controllers, AWSSDK.DynamoDBv2 (low-level), xUnit + NSubstitute + Shouldly + Testcontainers; Angular 22 (zoneless, signals, Material), Vitest.

**Spec:** `docs/superpowers/specs/2026-11-10-story-2-see-my-tasks-design.md`; also read `.claude/rules/rules.md`.

## Global Constraints

- **Rules:** follow `.claude/rules/rules.md`:
  - TDD (red, then green);
  - test names `Method_condition_expected` in .NET, and `'<Unit>.<method> <behaviour>'` in Angular to match the existing specs;
  - Conventional Commits;
  - `dotnet build TaskManager.slnx -warnaserror` clean, and `ng lint --max-warnings 0` clean;
  - no symlinks.
- **Layering:** no `Amazon.*` or `Microsoft.AspNetCore.*` in Domain or Application (the architecture tests must stay green). Handlers stay sealed and implement `IQueryHandler<,>`.
- **Endpoint:** `GET /api/v1/tasks?limit=&cursor=` returns `200 { "items": TaskResponse[], "nextCursor": string | null }`, newest first.
- **`limit`:** default `20`, valid range `1..100`. Field error key `limit`, message `"Limit must be between 1 and 100."`.
- **`cursor`:** opaque base64url (no padding) of JSON `{"userId":"…","_id":"…","createdAtId":"…"}`. Field error key `cursor`, message `"Cursor is invalid."`. Never `500`.
- **Index:** name `ByCreatedAt`; sort-key attribute `createdAtId` = `createdAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) + "#" + id.ToString("D")`.
- **UI copy:** button `"Load more"`, and while loading `"Loading…"`. Status region text `"Loading more tasks…"`. Error text `"Couldn't load more tasks."`. The empty state keeps `"No tasks yet — add your first one"`.
- **Coverage:** Domain + Application line coverage stays at 80% or more. No browser console warnings or errors.

## Review Focus

1. **Cursor that is valid base64 but carries the wrong JSON** (a number for `userId`, an extra key, a missing key) → `InvalidCursorException` → `400 cursor`, never `500`. *Test in Task 2, `TaskCursorTests`.*
2. **A very long or garbage `cursor` query string** (10 000 characters, non-base64 characters) → `400 cursor` without decoding the full input. Reject cursors longer than 1024 characters before decoding. *Test in Task 2 (`TaskCursorTests`) and Task 3 (API).*
3. **`limit` at the boundaries:** `1` and `100` are accepted and return at most that many items. *Test in Task 2 (handler) and Task 3 (API, `?limit=1`).*
4. **Double-clicking "Load more"** while a page is loading → exactly one API call, and no duplicate rows. *Test in Task 4 (store).*
5. **Adding a task, then "Load more"** → the new task stays first and appears once. *Test in Task 4 (store).*

---

### Task 1: Store `createdAtId` and declare the `ByCreatedAt` LSI

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Infrastructure/Persistence/TaskItemMapper.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Infrastructure/Persistence/TasksTableDefinition.cs`
- Test: `tests/TaskManager.Tasks.Infrastructure.Tests/TaskItemMapperTests.cs`
- Test (create): `tests/TaskManager.Tasks.Infrastructure.Tests/TasksTableDefinitionTests.cs`

**Interfaces:**
- Produces:
  - `public const string CreatedAtIdAttribute = "createdAtId";` and `public static string CreatedAtId(TaskItem task)` on `TaskItemMapper`.
  - `public const string ByCreatedAtIndex = "ByCreatedAt";` on `TasksTableDefinition`.

- [ ] **Step 1: Write the failing tests**

```csharp
// TaskItemMapperTests
[Fact]
public void ToItem_anyTask_writesCreatedAtIdAsUtcRoundTripAndId()
{
    var id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    var created = new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.FromHours(2));
    var task = TaskItem.Rehydrate(id, Guid.NewGuid(), "t", null, TaskItemStatus.ToDo, null, created, created);

    TaskItemMapper.ToItem(task)["createdAtId"].S
        .ShouldBe("2026-11-10T07:30:00.0000000+00:00#aaaaaaaa-0000-0000-0000-000000000001");
}

// TasksTableDefinitionTests
[Fact]
public void Create_anyName_declaresByCreatedAtLsiOnUserIdAndCreatedAtId()
{
    var lsi = TasksTableDefinition.Create("T").LocalSecondaryIndexes.ShouldHaveSingleItem();
    lsi.IndexName.ShouldBe("ByCreatedAt");
    lsi.KeySchema.Select(k => (k.AttributeName, k.KeyType.Value)).ShouldBe([("userId", "HASH"), ("createdAtId", "RANGE")]);
    lsi.Projection.ProjectionType.ShouldBe(ProjectionType.ALL);
}
```

- [ ] **Step 2: Run them and confirm they fail**

Run: `dotnet test tests/TaskManager.Tasks.Infrastructure.Tests --filter "ToItem_anyTask_writesCreatedAtIdAsUtcRoundTripAndId|Create_anyName_declaresByCreatedAtLsiOnUserIdAndCreatedAtId"`
Expected: FAIL (key `createdAtId` not found; `LocalSecondaryIndexes` empty).

- [ ] **Step 3: Implement.** `ToItem` writes `item["createdAtId"] = CreatedAtId(task).ToAttr()`. `CreatedAtId` follows the format in Global Constraints. `Create` adds `AttributeDefinition("createdAtId", S)` and a `LocalSecondaryIndex` named `ByCreatedAt` with the key schema above and `Projection { ProjectionType = ALL }`.

- [ ] **Step 4: Run the whole Tasks Infrastructure test project** (`dotnet test tests/TaskManager.Tasks.Infrastructure.Tests`). Expected: PASS. The Testcontainers tables are now created with the LSI.

- [ ] **Step 5: Commit** with `feat(tasks): store createdAtId and declare ByCreatedAt LSI`.

---

### Task 2: A paged list through the port, the handler and the DynamoDB repository

**Files:**
- Create: `src/Tasks/TaskManager.Tasks.Application/Ports/TaskPageResult.cs` (`TaskPageResult` record)
- Create: `src/Tasks/TaskManager.Tasks.Application/Ports/InvalidCursorException.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Application/Ports/ITaskRepository.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Application/ListTasks/ListTasks.cs` (query, `TaskPage`, handler)
- Modify: `src/Tasks/TaskManager.Tasks.Application/DependencyInjection.cs`
- Create: `src/Tasks/TaskManager.Tasks.Infrastructure/Persistence/TaskCursor.cs` (`internal static`; add `InternalsVisibleTo` for the test project in the csproj if it isn't there)
- Modify: `src/Tasks/TaskManager.Tasks.Infrastructure/Persistence/DynamoDbTaskRepository.cs`
- Modify: `src/Tasks/TaskManager.Tasks.Infrastructure/Seeding/TaskSeeder.cs`
- Test: `tests/TaskManager.Tasks.Application.Tests/TaskQueryHandlerTests.cs`
- Test (create): `tests/TaskManager.Tasks.Infrastructure.Tests/TaskCursorTests.cs`
- Test: `tests/TaskManager.Tasks.Infrastructure.Tests/DynamoDbTaskRepositoryTests.cs`
- Test: `tests/TaskManager.Tasks.Infrastructure.Tests/TaskSeederTests.cs`

**Interfaces:**
- Consumes (from Task 1): `TaskItemMapper.CreatedAtIdAttribute`, `TasksTableDefinition.ByCreatedAtIndex`.
- Produces:
  - `public sealed record TaskPageResult(IReadOnlyList<TaskItem> Items, string? NextCursor);`
  - `public sealed class InvalidCursorException(string message) : Exception(message);`
  - `ITaskRepository.ListByUserAsync(Guid userId, int limit, string? cursor, CancellationToken ct)` → `Task<TaskPageResult>` (replaces the old overload).
  - `public sealed record ListTasksQuery(Guid UserId, int? Limit, string? Cursor);`
  - `public sealed record TaskPage(IReadOnlyList<TaskResponse> Items, string? NextCursor);`
  - `ListTasksHandler : IQueryHandler<ListTasksQuery, TaskPage>`, with `public const int DefaultLimit = 20, MaxLimit = 100;`
  - `TaskCursor.Encode(IReadOnlyDictionary<string, AttributeValue> lastKey)` → `string`.
  - `TaskCursor.Decode(string cursor, Guid userId)` → `Dictionary<string, AttributeValue>`; throws `InvalidCursorException`.

- [ ] **Step 1: Write the failing handler tests** (replace `ListTasks_HandleAsync_multipleTasks_returnsNewestFirst`):

```csharp
[Fact] ListTasks_HandleAsync_noLimit_requestsDefault20AndMapsPage()
//   repo.ListByUserAsync(_userId, 20, null, ...) returns TaskPageResult([task], "next")
//   → result.Value.Items.Single().Id == task.Id; result.Value.NextCursor == "next"
[Theory, InlineData(1), InlineData(100)] ListTasks_HandleAsync_boundaryLimit_passesItThrough(int limit)
[Theory, InlineData(0), InlineData(101), InlineData(-1)] ListTasks_HandleAsync_outOfRangeLimit_returnsLimitFieldErrorWithoutQuerying(int limit)
//   → Error.Type == Validation; FieldErrors["limit"] == ["Limit must be between 1 and 100."]; repo DidNotReceive
[Fact] ListTasks_HandleAsync_repositoryRejectsCursor_returnsCursorFieldError()
//   repo throws InvalidCursorException → FieldErrors["cursor"] == ["Cursor is invalid."]
```

- [ ] **Step 2: Write the failing `TaskCursorTests`** (pure unit tests, no container):
  - `EncodeDecode_validKey_roundTrips`;
  - `Decode_otherUsersCursor_throwsInvalidCursor`;
  - `Decode_notBase64_throwsInvalidCursor` with input `"%%%"`;
  - `Decode_wrongJsonShape_throwsInvalidCursor`, a Theory over base64url of `{"userId":1,"_id":"x","createdAtId":"y"}`, `{"userId":"<u>","_id":"x"}` and `{"userId":"<u>","_id":"x","createdAtId":"y","extra":"z"}`;
  - `Decode_over1024Chars_throwsInvalidCursorWithoutDecoding` with `new string('A', 1025)`.

- [ ] **Step 3: Rewrite the repository list tests** (Testcontainers):
  - `ListByUserAsync_25Tasks_returnsNewestFirstPagesOf20Then5`. Add 25 tasks with `createdAt = base.AddSeconds(i)`. Page 1 has 20 items, ordered by `CreatedAt` descending, with a non-null cursor. Page 2 has 5 items. Then `nextCursor` is null, or one more call returns 0 items and a null cursor.
  - `ListByUserAsync_sameCreatedAt_ordersByIdDescending`. Two tasks with the same `createdAt` and ids `…01` and `…02` → `…02` comes first.
  - `ListByUserAsync_otherUsersTasks_neverAppearOnAnyPage`. Users `a` (3 tasks) and `b` (3 tasks), `limit` 2: walking every page of `a` sees only `a`.
  - `ListByUserAsync_cursorFromAnotherUser_throwsInvalidCursor`.
  - Delete the old `ListByUserAsync_pages_through_more_than_one_page` and `ListByUserAsync_returns_only_that_users_tasks`; the tests above replace them.
  - In `TaskSeederTests`, read with `ListByUserAsync(DemoId, 100, null, default)`.

- [ ] **Step 4: Run all three test projects and confirm they fail** (`dotnet test tests/TaskManager.Tasks.Application.Tests` and `dotnet test tests/TaskManager.Tasks.Infrastructure.Tests`). Expected: compile errors or FAIL on the new members.

- [ ] **Step 5: Implement.**
  - **Handler:** validate the limit first, then call the repository. Catch `InvalidCursorException` and map it to the cursor field error. Map items with `ToResponse()`; there's no sorting in the handler any more.
  - **DI:** update the registration to `IQueryHandler<ListTasksQuery, TaskPage>`.
  - **`TaskCursor`:** `JsonSerializer` over a `Dictionary<string, string>`. Require exactly the keys `userId`, `_id` and `createdAtId`, all strings. `userId` must equal the caller's `ToString("D")`. Use base64url via `Convert.ToBase64String`/`FromBase64String` with a `+/` ↔ `-_` swap and trimmed padding (Infrastructure has no ASP.NET reference). Wrap `FormatException` and `JsonException` in `InvalidCursorException`. Check the length before doing anything else.
  - **Repository:** one `QueryAsync` with `IndexName = ByCreatedAtIndex`, `ConsistentRead = true`, `ScanIndexForward = false`, `Limit = limit`, `ExclusiveStartKey = cursor is null ? null : TaskCursor.Decode(cursor, userId)`. `NextCursor` = `LastEvaluatedKey is { Count: > 0 } ? Encode(...) : null`.
  - **Seeder:** `(await repository.ListByUserAsync(userId, 1, null, ct)).Items.Count > 0`.

- [ ] **Step 6: Run both projects plus `tests/TaskManager.ArchitectureTests`.** Expected: PASS.

- [ ] **Step 7: Commit** with `feat(tasks): page the task list with a cursor over the ByCreatedAt index`.

---

### Task 3: API contract for the paged list

**Files:**
- Modify: `src/Tasks/TaskManager.Tasks.Api/Controllers/TasksController.cs` (`List`)
- Test: `tests/TaskManager.Tasks.Api.Tests/TasksEndpointsTests.cs`, `tests/TaskManager.Tasks.Api.Tests/TasksApiFactory.cs` (`List` substitute type becomes `IQueryHandler<ListTasksQuery, TaskPage>`)

**Interfaces:**
- Consumes (from Task 2): `ListTasksQuery(Guid UserId, int? Limit, string? Cursor)`, `TaskPage`, `IQueryHandler<ListTasksQuery, TaskPage>`.
- Produces: `GET /api/v1/tasks?limit=&cursor=` → `{ items, nextCursor }`.

- [ ] **Step 1: Write the failing tests** (replace `List_returns_200_with_tasks_for_caller`):
  - `List_withToken_returnsItemsAndNextCursor`. Stub `List.HandleAsync(new ListTasksQuery(userId, null, null), …)` to return a page of 2 with `"c1"`. The body has `items` with length 2 and `nextCursor == "c1"`.
  - `List_withLimitAndCursor_passesThemFromQueryAndOwnerFromToken`. Call `GET /api/v1/tasks?limit=1&cursor=abc&userId=<other>` and expect `Received(1)` for `new ListTasksQuery(userId, 1, "abc")`.
  - `List_nonIntegerLimit_returns400WithLimitError` (`?limit=abc`): the `errors.limit` key exists.
  - `List_limitOutOfRange_returns400` with real handlers: `new TasksApiFactory(useRealHandlers: true)` and `?limit=500` give `errors.limit`.
  - `List_invalidCursor_returns400` with real handlers and `?cursor=` set to a 2000-character string give `errors.cursor`.

- [ ] **Step 2: Run them and confirm they fail** (`dotnet test tests/TaskManager.Tasks.Api.Tests`).

- [ ] **Step 3: Implement** `List([FromQuery] int? limit, [FromQuery] string? cursor, [FromServices] IQueryHandler<ListTasksQuery, TaskPage> handler, CancellationToken ct)`. It builds `new ListTasksQuery(CurrentUserId, limit, cursor)`. The response body is `TaskPage`, which serializes to camelCase `items` and `nextCursor`.

- [ ] **Step 4: Run the full backend suite** (`dotnet build TaskManager.slnx -warnaserror && dotnet test TaskManager.slnx`). Expected: 0 warnings, all PASS.

- [ ] **Step 5: Commit** with `feat(tasks-api): return paged task list with limit and cursor`.

---

### Task 4: Angular API and store paging

**Files:**
- Modify: `web/src/app/features/tasks/data/task.model.ts` (add `TaskPage`)
- Modify: `web/src/app/features/tasks/data/tasks-api.ts`, `tasks-api.spec.ts`
- Modify: `web/src/app/features/tasks/data/tasks-store.ts`, `tasks-store.spec.ts`

**Interfaces:**
- Consumes (from Task 3): the response `{ items: Task[]; nextCursor: string | null }`.
- Produces:
  - `export interface TaskPage { items: Task[]; nextCursor: string | null }`;
  - `TasksApi.list(params?: { limit?: number; cursor?: string }): Observable<TaskPage>`;
  - on `TasksStore`: `tasks`, `loading`, `error`, `loadingMore: Signal<boolean>`, `loadMoreError: Signal<AppError | null>`, `hasMore: Signal<boolean>`, `load(): Promise<void>`, `loadMore(): Promise<void>`, `create(req)` (unchanged).

- [ ] **Step 1: Write the failing specs.**
  - **`tasks-api.spec.ts`:**
    - `list GETs /api/v1/tasks without params by default`;
    - `list sends cursor and limit as query params`. Expect `req.request.params.get('cursor') === 'c1'` and `get('limit') === '5'`.
  - **`tasks-store.spec.ts`:** change the `api.list` mock type to `Observable<TaskPage>`, then add:
    - `TasksStore.load replaces tasks and stores nextCursor` (`hasMore()` is true);
    - `TasksStore.loadMore appends the next page and clears hasMore on the last page`;
    - `TasksStore.loadMore skips tasks already in the list`;
    - `TasksStore.loadMore does nothing without a cursor`;
    - `TasksStore.loadMore ignores a second call while one is in flight`. Use a `Subject` and call it twice: `api.list` is called once.
    - `TasksStore.loadMore keeps tasks and cursor and sets loadMoreError on failure`;
    - `TasksStore.create then loadMore keeps the new task first and once`.

- [ ] **Step 2: Run them and confirm they fail** (`cd web && npx ng test --watch=false`).

- [ ] **Step 3: Implement.**
  - `list` uses `HttpParams` and only sets the keys that are defined.
  - The store adds the `nextCursor` signal plus `loadingMore` and `loadMoreError`, and `hasMore = computed(() => this.nextCursor() !== null)`.
  - `load` resets `nextCursor` and `loadMoreError`.
  - `loadMore` guards against `loadingMore()` or a missing cursor, appends with an id-set dedupe, and on error sets `loadMoreError` without touching `tasks` or the cursor.

- [ ] **Step 4: Run** `npx ng test --watch=false && npx ng lint --max-warnings 0`. Expected: PASS and lint clean.

- [ ] **Step 5: Commit** with `feat(web): page tasks through the API cursor in the tasks store`.

---

### Task 5: "Load more" UI and the empty-state rule

**Files:**
- Modify: `web/src/app/features/tasks/tasks-page/tasks-page.html`, `tasks-page.ts` (import `MatButtonModule`), `tasks-page.scss`, `tasks-page.spec.ts`
- Modify: `web/src/app/features/tasks/task-list/task-list.html`, `task-list.ts` (empty state moves to the page), `task-list.spec.ts`

**Interfaces:**
- Consumes (from Task 4): `store.hasMore()`, `store.loadingMore()`, `store.loadMoreError()`, `store.loadMore()`.

- [ ] **Step 1: Write the failing specs** (`tasks-page.spec.ts`; update the list mock to return a `TaskPage`):
  - `TasksPage: shows Load more only when there are more tasks`;
  - `TasksPage: clicking Load more appends the next page`;
  - `TasksPage: disables Load more and shows Loading… while loading`;
  - `TasksPage: shows "Couldn't load more tasks." and keeps the list when loading more fails`;
  - `TasksPage: shows the empty message only when the first page is empty`;
  - `TaskList: renders nothing when given no tasks` (replaces `TaskList: renders empty state when no tasks`).

- [ ] **Step 2: Run them and confirm they fail** (`npx ng test --watch=false`).

- [ ] **Step 3: Implement.**
  - **Page template:** when `store.tasks().length === 0`, it shows `<app-empty-state message="No tasks yet — add your first one" />`. Otherwise it shows `<app-task-list>` followed by, if `store.hasMore()`, a `mat-stroked-button` labelled `Load more` (or `Loading…`), `[disabled]="store.loadingMore()"`, `(click)="store.loadMore()"`.
  - Add a `<p role="status" aria-live="polite">` holding `Loading more tasks…` while loading more, and a `<p class="alert" role="alert">Couldn't load more tasks.</p>` when `loadMoreError()` is set.
  - `TaskList` renders only the `<ul>`, and drops its `EmptyState` import.
  - Use the existing alert and status styles. Make the button full width at 375px and auto width on desktop.

- [ ] **Step 4: Run** `npx ng test --watch=false && npx ng lint --max-warnings 0 && npx ng build`. Expected: all PASS, no warnings.

- [ ] **Step 5: Commit** with `feat(web): add Load more to the task list`.

---

### Task 6: Docs, acceptance walkthrough and PR

**Files:**
- Modify: `README.md`, `GENAI.md`, `docs/superpowers/specs/2026-11-10-story-1-foundation-design.md` (§11)

- [ ] **Step 1: Update `README.md`.**
  - The `GET /api/v1/tasks` API row: `limit` (1–100, default 20), `cursor`, and the response `{ items, nextCursor }`.
  - Known limitations: drop "No pagination parameters on the list endpoint" (keep "no password reset").
  - Quick start: add "After pulling, run `docker compose down && docker compose up -d` so the Tasks table is recreated with its index."
  - Add a `## Project status` section after the intro. It holds a table of sub-project 1 (merged in #29), Story 2 (#16, in review) and the remaining stories (pending), plus "Last updated: 2026-11-10".

- [ ] **Step 2: Update the Story 1 spec §11** to delete the "Task list is unbounded" bullet. Add to `GENAI.md` (corrections) one bullet on Story 2: what was already met, why pagination with an LSI, and the evidence.

- [ ] **Step 3: Do the full verification.**
  - Run `dotnet build TaskManager.slnx -warnaserror`, `dotnet test TaskManager.slnx` with the coverage commands from the README (Domain + Application ≥ 80%), and `cd web && npx ng lint --max-warnings 0 && npx ng test --watch=false && npx ng build`.
  - Expected: everything green.

- [ ] **Step 4: Do the browser walkthrough.**
  - Restart DynamoDB Local, then start `users-api`, `tasks-api` and `web` with the `.claude/launch.json` previews.
  - Check, recording the results for the PR:
    - AC1: demo sign-in shows the seeded tasks;
    - AC2: add a task, sign out, sign in, and it's still there;
    - AC3: a new user sees the empty message;
    - paging: create 25 tasks through the API with the demo token, then 20 show, "Load more" adds 5 and the button disappears;
    - the 375px layout;
    - the console is clean.

- [ ] **Step 5: Commit** (`docs: document paged task list and Story 2 status`), push, and open a PR with the title `feat: Story 2 — see my tasks with paged list` and the body `Closes #16`.
  - The body has an acceptance-evidence table that maps each AC to its tests and the walkthrough result.
  - End the body with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
  - Per the user's instruction, merge once **every** check (`backend`, `frontend`, `repo-hygiene`, `review`) is green. Confirm each one first: `main` has no branch protection, so `--auto` would merge at once. Merge with `gh pr merge <n> --merge --match-head-commit <full sha>`.
