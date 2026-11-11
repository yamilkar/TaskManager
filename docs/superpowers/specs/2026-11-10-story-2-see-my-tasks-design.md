# Story 2: See my tasks — design

- **Issue:** yamilkar/TaskManager#16
- **Branch:** `feature/story-2-see-my-tasks`
- **Builds on:** sub-project 1, merged in PR #29 (`2026-11-10-story-1-foundation-design.md`); its decisions D1–D10 still hold.
- **Status:** revised on 2026-11-10. Pagination was removed from scope: it is not a requirement of the story and added complexity.

## 1. Context and scope

### 1.1 Story (from #16)

**As** a user, **I want** to see all the tasks I've added, **so that** I know what's on my plate.

| # | Acceptance criterion | State before this story |
|---|---|---|
| AC1 | After signing in I see my task list | Already met: login goes to `/tasks`, and `TasksPage` loads `GET /api/v1/tasks` |
| AC2 | My tasks are still there the next time I sign in | Already met: tasks live in DynamoDB under the user's id |
| AC3 | If I have no tasks, I see a friendly empty message | Already met: `TaskList` shows "No tasks yet — add your first one" |

### 1.2 What this story adds

1. **Acceptance evidence.** Each criterion maps to automated tests plus a browser walkthrough, so #16 is closed on evidence rather than on assumption.
2. **A stable order.** The list is newest first, and tasks created at the same instant are ordered by id, so the order is the same on every sign-in.

### 1.3 Out of scope

| Item | Where it belongs |
|---|---|
| Pagination, "Load more", infinite scroll | Not planned. The whole list is returned and the user scrolls |
| Filter by status, due-soonest ordering | Story 8 (#22) |
| Description, status and due-date rules | Story 3 (#17), already implemented in sub-project 1 |
| Update and delete | Stories 4 and 5 (#18, #19) |

## 2. Decisions

| # | Decision | Reason |
|---|---|---|
| D11 | **No pagination.** `GET /api/v1/tasks` keeps returning the caller's whole list as a JSON array | Not a requirement of the story. The repository already reads every DynamoDB page with consistent reads; a personal task list stays small. KISS and YAGNI |
| D12 | **Newest first, ties broken by id descending**, sorted in `ListTasksHandler` | Ordering is a rule of the use case, so it lives in Application and is unit-tested. The table's sort key is `_id`, so DynamoDB returns items in id order, not time order; the handler sorts them. The id tie-break keeps the order deterministic |

## 3. API contract

`GET /api/v1/tasks` (requires a JWT): unchanged from Story 1. `200` with an array of `TaskResponse`, newest first by `createdAt`, ties by `id` descending. The owner comes from the token's `sub`.

## 4. Design

- **Application:** `ListTasksHandler` orders by `CreatedAt` descending, then by `Id` descending. No other change.
- **Infrastructure, Api, Angular:** unchanged from Story 1. The table keeps its key schema (`userId` + `_id`), with no secondary index.

## 5. Testing

| Layer | Tests |
|---|---|
| Application | `ListTasks_HandleAsync_multipleTasks_returnsNewestFirst`; `ListTasks_HandleAsync_sameCreatedAt_ordersByIdDescending` (new) |
| Infrastructure (Testcontainers) | Existing: only the caller's tasks, more than one DynamoDB page read in full, persisted with consistent reads |
| Api | Existing: list returns `200` with the caller's tasks, owner from the token |
| Angular | Existing: `TasksPage` loads tasks on init, a new task appears first, `TaskList` shows the empty message |

### 5.1 Acceptance evidence for #16 (recorded in the PR description)

| Criterion | Automated evidence | Browser walkthrough |
|---|---|---|
| AC1: list after sign-in | `TasksPage` spec; API list test | Sign in as the demo user; the seeded tasks show, newest first |
| AC2: still there next time | Repository tests (persisted, consistent reads); API tests | Sign out and back in; same tasks, same order |
| AC3: friendly empty message | `TaskList` spec | Register a new user and see the empty message |

## 6. Documentation updates

- **README:** the **Project status** section, kept current.
- **`GENAI.md`:** a short entry on the scope decision.
