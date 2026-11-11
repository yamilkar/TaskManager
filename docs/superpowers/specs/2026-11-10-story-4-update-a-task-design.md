# Design Spec — Story 4: Update a task

- **Date:** 2026-11-10
- **Status:** Draft — awaiting human review (open questions in section 7)
- **Issue:** yamilkar/TaskManager#18 — *Story 4: Update a task*
- **Builds on:** [Story 1 foundation spec](2026-11-10-story-1-foundation-design.md) (field rules 4.3, D9, D10 CQRS). That spec lists update and delete together as sub-project 2. Story 3 used D16–D20, so this spec starts at D21.

---

## 1. Story and acceptance criteria

**As** a user, **I want** to edit a task and move it between To do, In progress and Done, **so that** my list reflects reality.

- AC1: I can change any detail of a task I created.
- AC2: I can move a task between To do, In progress and Done.
- AC3: The same rules as when adding a task apply, and I'm told what to fix.

## 2. What exists on `main`

`TaskItem.Create` holds every field rule. `POST`, `GET` (list) and `GET /{id}` endpoints exist. `ITaskRepository` has `AddAsync`, `ListByUserAsync` and `GetAsync`. The Angular `TaskForm` creates tasks only, and `TaskList` is read only. There is no update path in any layer.

## 3. Decisions

| # | Decision | Reason |
|---|---|---|
| D21 | **`PUT /api/v1/tasks/{id}`** replaces the editable fields: `{ title, description, status, dueDate }`, the same body as create. It returns `200` with the `TaskResponse`. A missing task, or another user's task, returns `404`. Rule failures return `400` `ValidationProblemDetails` with the same keys and messages as create | One command covers AC1 and AC2. PUT with the full field set needs no PATCH merge rules (KISS). `404` for another user's task matches `GET` and does not reveal that the task exists |
| D22 | `TaskItem.Update(title, description, status, dueDate, now)` returns a **new** `Result<TaskItem>`. It keeps `Id`, `UserId` and `CreatedAt`, sets `UpdatedAt = now`, and shares one private validation routine with `Create` | AC3 "same rules": one source of truth, so Story 3's message changes apply to both paths automatically. The entity stays immutable |
| D23 | On update, `status` is **required**. A missing status returns `status: ["Status must be one of: ToDo, InProgress, Done."]` (`TaskItemStatuses.InvalidMessage`) | On create, a missing status means a new task (`ToDo`, D17). On a full replace, a missing status would silently reset the task to To do |
| D24 | The past-due-date rule (D9) applies **only when the due date changes**. An unchanged due date passes even if it is now in the past. The form does the same: it accepts the task's current due date | An overdue task must still be renamed or moved to Done. Otherwise AC2 breaks for exactly the tasks that need it most |
| D25 | Clearing a field is allowed: a blank `description` becomes `null`, and `dueDate: null` removes the due date. The write is a conditional `PutItem` (`attribute_exists(_id)`). If the item is gone, the handler returns `404` and creates nothing | A plain `PutItem` would recreate a task deleted in another tab (Story 5). The `userId`+`_id` key keeps ownership |
| D26 | Concurrent edits: **last write wins**. No version check or ETag | YAGNI. One user editing their own tasks; no story asks for conflict detection |
| D27 | UI: **one form, two modes.** Each list row gets an **Edit** button. It loads that task into the existing form card ("Edit task", **Save changes**, **Cancel**) and focuses Title. Each row also gets a **Move** menu with the other two statuses, which saves at once. Snackbars: "Task updated", "Moved to In progress" (the status label). A failed move shows "Couldn't move the task: <reason>" and leaves the list unchanged | Reuses the existing form, validators and server-error mapping (DRY). The menu makes AC2 one click without opening the form. A menu has no DOM state to revert after a failure, unlike a `<select>` |
| D28 | Metric `TaskUpdated` is incremented on each successful update | Same pattern as `TaskCreated` |

## 4. Changes

- **Domain:** `TaskItem.Update` (D22–D25).
- **Application:** `UpdateTask/UpdateTask.cs` (command and handler), `ITaskRepository.UpdateAsync`, DI registration, `TasksMetrics.TaskUpdated`.
- **Infrastructure:** `DynamoDbTaskRepository.UpdateAsync` (conditional put).
- **Api:** `UpdateTaskRequest` and `TasksController.Update`.
- **Angular:** `UpdateTaskRequest` type, `TasksApi.update`, `TasksStore.update`, the `TaskForm` edit mode, `TaskList` Edit and Move actions, `TasksPage` wiring, and `STATUS_LABELS`, moved from `status-badge` to `task.model.ts` and exported.
- **Docs:** README capability table and **Project status** row.

## 5. Testing (TDD, per layer)

Domain: the rules on update, D23 and D24. Application: not found, validation, persisting, the item vanishing between read and write, and the metric. Infrastructure (DynamoDB Local): replace, remove cleared attributes, no upsert. Api: `401`, `sub` used as owner, body ids ignored, `200`/`400`/`404`, a bad date format, a non-GUID id. Angular: api, store, form edit mode, list actions, page flows. The Domain and Application coverage gate (≥ 80%) still applies.

## 6. Acceptance evidence

| AC | Automated | Manual (local stack, demo account) |
|---|---|---|
| AC1 | `Update_valid_body_returns_200_with_changes`, `TasksPage: edit saves changes in place` | Edit a task's title, description and due date, save, and see the row change in place |
| AC2 | `Update_status_only_change_returns_200`, `TasksPage: move menu changes status` | Move a task To do → In progress → Done using the menu |
| AC3 | `Update_blank_title_returns_400_with_title_error`, `Update_new_past_due_date_returns_400`, `TaskForm: edit mode shows the same validation messages` | Clear the title and save: "Title is required." appears and the task is unchanged |
| All | CI green (`backend`, `frontend`, `repo-hygiene`) | Zero console warnings or errors on `/tasks`; screenshots attached to the PR |

## 7. Open questions for the user

1. **Q1 — Move menu (D27).** Recommendation: keep the per-row Move menu, so a status change takes one click. Alternative: change status only in the edit form (less UI, but AC2 then needs Edit → select → Save).
2. **Q2 — Overdue tasks (D24).** Recommendation: accept an unchanged past due date. Alternative: always enforce the rule, so the user must clear or move the date before any other edit.
3. **Q3 — Missing status on PUT (D23).** Recommendation: `400`. Alternative: keep the current status when it is missing (PATCH-like).

## 8. Out of scope

Deleting a task (Story 5); filtering and sorting (Story 8); edit history; optimistic concurrency; inline editing in the row; keyboard shortcuts.
