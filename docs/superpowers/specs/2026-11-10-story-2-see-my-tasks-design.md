# Story 2: See my tasks — design

- **Issue:** yamilkar/TaskManager#16
- **Branch:** `feature/story-2-see-my-tasks`
- **Builds on:** sub-project 1, merged in PR #29 (`2026-11-10-story-1-foundation-design.md`); its decisions D1–D10 still hold.
- **Status:** approved in conversation on 2026-11-10; this document is waiting for review.

## 1. Context and scope

### 1.1 Story (from #16)

**As** a user, **I want** to see all the tasks I've added, **so that** I know what's on my plate.

| # | Acceptance criterion | State before this story |
|---|---|---|
| AC1 | After signing in I see my task list | Already met: login goes to `/tasks`, and `TasksPage` loads `GET /api/v1/tasks` |
| AC2 | My tasks are still there the next time I sign in | Already met: tasks live in DynamoDB under the user's id |
| AC3 | If I have no tasks, I see a friendly empty message | Already met: `TaskList` shows "No tasks yet — add your first one" |

### 1.2 What this story adds

1. **Acceptance evidence.** Each criterion maps to automated tests plus a recorded browser walkthrough, so #16 can be closed on evidence rather than on assumption.
2. **Pagination of the task list** (decided in the PR #29 review, where the unbounded list was flagged). The API returns pages, and the Angular list gets a **"Load more"** button.

### 1.3 Out of scope

| Item | Where it belongs |
|---|---|
| Filter by status, due-soonest ordering | Story 8 (#22) |
| Description, status and due-date rules | Story 3 (#17), already implemented in sub-project 1 |
| Update and delete | Stories 4 and 5 (#18, #19) |
| Total count, page numbers, infinite scroll | Not planned. "Load more" was chosen; DynamoDB has no cheap total count |
| Rate limits per instance, multi-hop forwarded headers | Sub-project 3 (spec §11 of the Story 1 design) |

## 2. Decisions

| # | Decision | Reason |
|---|---|---|
| D11 | Page the list with a **local secondary index** `ByCreatedAt` (partition key `userId`, sort key `createdAtId`) and an opaque cursor | An LSI supports `ConsistentRead`, so a task the user just created always appears on reload; a GSI is only eventually consistent. An LSI shares the table's capacity. It can only be defined at table creation, which costs nothing now because no deployed table exists yet (deployment is sub-project 3) and local tables are recreated from code. The 10 GB per-partition limit is per user, far beyond any task list. Alternatives rejected: a GSI (eventual consistency, separate capacity), and slicing an in-memory list (a paged contract with no cost reduction). |
| D12 | `createdAtId` = `"<createdAt as UTC ISO-8601 round-trip ('O')>#<_id>"` | ISO-8601 in UTC sorts lexically in time order; the `#<_id>` suffix makes keys unique when two tasks share a timestamp, so ordering and paging are stable. |
| D13 | The cursor is opaque: base64url of the JSON `LastEvaluatedKey` (`userId`, `_id`, `createdAtId`) | The client only stores and returns it. On decode the server checks that it has exactly those three keys and that `userId` equals the caller's, so a cursor can never read another user's tasks. |
| D14 | "Load more" UX, page size 20 by default, `limit` 1–100 | It maps directly to a cursor, works well on phones (Story 9), and is easy to test and make accessible. |
| D15 | `GET /api/v1/tasks` changes from a bare array to `{ items, nextCursor }` at the same version | An array cannot carry the cursor. The Angular app is the only consumer and changes in the same PR, so no `/v2` is needed. |

## 3. API contract

`GET /api/v1/tasks?limit=<1..100>&cursor=<opaque>` (requires a JWT)

| Query parameter | Rule |
|---|---|
| `limit` | Optional. Default `20`. An integer from 1 to 100; otherwise `400` with a field error on `limit` ("Limit must be between 1 and 100."). |
| `cursor` | Optional. Taken from a previous response's `nextCursor`. If it can't be decoded, doesn't have the expected keys, or belongs to another user: `400` with a field error on `cursor` ("Cursor is invalid."). Never `500`. |

Response `200`:

```json
{
  "items": [ { "id": "…", "title": "…", "description": null, "status": "ToDo", "dueDate": null, "createdAt": "…", "updatedAt": "…" } ],
  "nextCursor": "eyJ1c2VySWQiOi…"
}
```

- `items` are `TaskResponse` objects (unchanged shape), **newest first** by `createdAt`, ties broken by `_id` descending.
- `nextCursor` is `null` when DynamoDB reports no further key. DynamoDB may return a key when the page ends exactly on the last item; the client then gets one extra, empty page. This is accepted (avoiding it would cost an extra read) and is covered by tests.
- The owner always comes from the token's `sub`; there is no `userId` query parameter, and one sent anyway is ignored.
- Errors are RFC 9457 ProblemDetails, as everywhere else.

## 4. Backend design

### 4.1 Storage (`TaskManager.Tasks.Infrastructure`)

- `TaskItemMapper.ToItem` also writes `createdAtId` (D12). `FromItem` ignores it. The seeder writes through the mapper, so seeded tasks get it too.
- `TasksTableDefinition` adds `AttributeDefinition("createdAtId", S)` and `LocalSecondaryIndexes = [ByCreatedAt]`, with key schema `userId` (HASH) + `createdAtId` (RANGE) and projection `ALL`.
- `DynamoDbTaskRepository.ListByUserAsync(userId, limit, cursor, ct)`:
  - runs **one** `Query` on `IndexName = "ByCreatedAt"` with `KeyConditionExpression = "userId = :u"`, `ConsistentRead = true`, `ScanIndexForward = false`, `Limit = limit`, and `ExclusiveStartKey` = the decoded cursor;
  - returns `TaskPageResult(IReadOnlyList<TaskItem> Items, string? NextCursor)`, encoding `LastEvaluatedKey` when it is present.
- `TaskCursor` (internal to Infrastructure) owns encoding and decoding (D13). Decoding failures, unexpected keys or another user's id throw `InvalidCursorException`.
- **Local development:** DynamoDB Local runs `-inMemory`, so `docker compose down && docker compose up -d` recreates the table with the index. `TableBootstrapper` does not alter an existing table; the README says to restart.

### 4.2 Application (`TaskManager.Tasks.Application`)

- **Port:** `ITaskRepository.ListByUserAsync(Guid userId, int limit, string? cursor, CancellationToken ct)` returns `Task<TaskPageResult>`. `TaskPageResult` and `InvalidCursorException` live in `Ports/`, with no AWS types (the architecture tests keep enforcing this).
- **Query:** `ListTasksQuery(Guid UserId, int? Limit, string? Cursor)` → `IQueryHandler<ListTasksQuery, TaskPage>`.
- **Response:** `TaskPage(IReadOnlyList<TaskResponse> Items, string? NextCursor)`.
- **`ListTasksHandler`:**
  1. Validates `Limit`: `null` → 20; outside 1..100 → validation error on `limit`.
  2. Calls the repository and maps the items with `ToResponse()`. There is no in-memory sort any more, because the index orders them.
  3. Catches `InvalidCursorException` → validation error on `cursor`.
- `ListTasksQuery` keeps `UserId` from the token, as before.

### 4.3 Api (`TaskManager.Tasks.Api`)

- `TasksController.List([FromQuery] int? limit, [FromQuery] string? cursor, handler, ct)` builds `ListTasksQuery(CurrentUserId, limit, cursor)` and returns `Ok(page)`.
- If `limit` is not an integer (`?limit=abc`), model binding returns the standard `400` ProblemDetails with a field error on `limit`.

## 5. Angular design (`web/src/app/features/tasks`)

- **`task.model.ts`:** adds `TaskPage = { items: Task[]; nextCursor: string | null }`.
- **`TasksApi.list(params?: { limit?: number; cursor?: string })`** returns `Observable<TaskPage>` and builds the query with `HttpParams` (no string concatenation). The page doesn't pass `limit`; the API default of 20 applies.
- **`TasksStore`** (signals, still provided by `TasksPage`):
  - **State:** `tasks`, `nextCursor`, `loading` (first page), `loadingMore`, `error` (first page), `loadMoreError`.
  - **Derived:** `hasMore = computed(() => nextCursor() !== null)`.
  - `load()` fetches the first page, replaces `tasks` and sets `nextCursor`.
  - `loadMore()` does nothing if `loadingMore()` is true or there is no cursor. Otherwise it fetches the next page and appends items whose `id` is not already in the list (a safety net), updating `nextCursor`. On failure it sets `loadMoreError` and keeps the existing tasks and cursor, so the button retries.
  - `create()` stays as it is: the new task goes first. A new task is newer than every page already loaded, so later pages never return it again, and the cursor needs no reset.
- **`TaskList`** is unchanged, apart from the empty state now being decided by the page (next point).
- **`TasksPage` template:**
  - It shows the list. Under it, while `store.hasMore()`, a Material button **"Load more"** appears. While loading it is disabled and reads "Loading…".
  - An `aria-live="polite"` status region announces "Loading more tasks…".
  - If `loadMoreError` is set, an inline `role="alert"` says "Couldn't load more tasks." and the button stays as the retry.
  - The empty message appears only when the first page is empty: no tasks, not loading and no error.
- **Unchanged:** routing, guards, interceptors, the add-task form and the "Task created" snackbar. It stays zoneless and `OnPush`, with signals, `@if`/`@for`, and no `.component` suffix.

## 6. Testing (TDD, names `Method_condition_expected`)

| Layer | Tests |
|---|---|
| Application (`ListTasksHandler`) | Passes `limit`/`cursor` through and maps to `TaskPage`; `null` limit → 20; `0`, `101`, `-1` → validation error on `limit`, repository not called; `InvalidCursorException` → validation error on `cursor` |
| Infrastructure (DynamoDB Local, Testcontainers) | 25 tasks → page 1 has 20 newest-first plus a cursor, page 2 has 5 with `nextCursor` null (or one extra empty page, accepted); equal `createdAt` values ordered by the `#id` tie-breaker; another user's tasks never appear on any page; a tampered cursor and a cursor minted for another user throw `InvalidCursorException` |
| Infrastructure (unit) | The mapper writes `createdAtId` in D12 format; the table definition declares the `ByCreatedAt` LSI; `TaskCursor` round-trips |
| Api | `GET /tasks` returns `{ items, nextCursor }`; `?limit=abc`, `?limit=500` and a bad cursor each return `400` ProblemDetails with the field error; the query is built from the token's `sub` (a `userId` query value is ignored) |
| Architecture | The existing rules still pass: no `Amazon.*` in Application, and handlers are sealed and implement the handler interfaces |
| Angular (Vitest) | `TasksApi.list` sends `cursor`/`limit` as query params; the store's `load` replaces, `loadMore` appends and skips duplicates, does nothing with no cursor or while already loading, and keeps tasks and cursor on failure; the page shows "Load more" only when `hasMore`, clicking it calls `loadMore`, and the empty state appears only for an empty first page |

Coverage on Domain + Application stays at 80% or more (it is 98.5% today). `ng lint --max-warnings 0` passes, and there are no console warnings or errors.

### 6.1 Acceptance evidence for #16 (recorded in the PR description)

| Criterion | Automated evidence | Browser walkthrough |
|---|---|---|
| AC1: list after sign-in | `TasksPage` and store tests; API list test | Sign in as the demo user; the seeded tasks show |
| AC2: still there next time | API and infrastructure tests (persisted, consistent reads) | Add a task, sign out, sign in, and the task is still there |
| AC3: friendly empty message | Page test for an empty first page | Register a new user and see the empty message |
| Paging | Infrastructure, API and store tests | With 25+ tasks: 20 show, "Load more" adds the rest, then the button disappears |

The walkthrough also checks the layout at 375px and on desktop, and that the browser console is clean.

## 7. Documentation updates

- **README:**
  - the `GET /api/v1/tasks` row gets the query parameters and the new response shape;
  - Known limitations drops "No pagination parameters on the list endpoint";
  - a note to restart `docker compose` after pulling (the table now has an index);
  - the **Project status** section, kept current as agreed.
- **Story 1 design spec, §11:** remove "Task list is unbounded" (resolved here).
- **`GENAI.md`:** a short entry on how Story 2 was designed and validated.

## 8. Risks and notes

- **Deployed tables:** the LSI must be defined when sub-project 3 creates the real table with infrastructure as code. An LSI can't be added to a live table, so the IaC must create the table from `TasksTableDefinition`'s shape.
- **Story 8:** due-soonest ordering can add a second LSI (`dueDateId`) the same way. An LSI can only be added when a table is created, and DynamoDB allows at most 5 LSIs per table. That is another reason to define Story 8's index before the first deployment.
