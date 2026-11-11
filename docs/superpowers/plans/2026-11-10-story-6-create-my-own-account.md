# Story 6: Create my own account — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A new visitor can sign up with an email and password, is told when the password is too weak, and ends up signed in on their own private task list.

**Architecture:** Most of this story shipped with Story 1: `POST /api/v1/auth/register` (`RegisterUserHandler`, `RegisterUserValidator`), the conditional unique-email write in `DynamoDbUserRepository.TryAddAsync`, the Angular `Register` page, and `AuthStore.register` (register, then log in). Tasks are partitioned by the JWT `sub`. This plan closes the gaps that remain against the acceptance criteria. It adds one backend key-flow test for sign-up followed by sign-in, aligns the weak-password copy and adds a hint, and handles the case where the account is created but the automatic sign-in fails. No new endpoints, projects or packages.

**Tech Stack:** .NET 10, ASP.NET Core, xUnit + Shouldly + NSubstitute. Angular standalone + signals, Angular Material, Vitest via `ng test`.

**Spec:** GitHub issue yamilkar/TaskManager#20 ("Story 6: Create my own account"). There is no separate design doc. The decisions below take its place.

**Branch:** `feature/story-6-create-my-own-account`, cut from `main` after Story 5 (`feature/story-5-delete-a-task`) merges.

## Acceptance criteria → coverage

| Criterion | Already covered by | This plan adds |
|---|---|---|
| An email can only be used once | `TryAddAsync_returns_EmailTaken_for_duplicate_email_and_writes_no_second_user`, `Concurrent_TryAddAsync_with_same_email_only_one_succeeds`, `HandleAsync_emailAlreadyRegistered_returnsConflict`, `Register_returns_409_problem_with_code_when_email_taken`, `Email.Create_normalizes`, web `Register: 409 sets server error on the email control` | Task 1: same email with different case and spaces → `409` through the real handler |
| I'm told if my password is too weak | `RegisterValidate_weakPassword_returnsPasswordError`, `Register_returns_400_validation_problem_with_field_errors` | Task 2: hint shown before typing, client copy matches server copy, web test for the weak-password message |
| After signing up I can sign in and start adding tasks | `AuthStore: register registers then logs in`, `Register: success navigates to /tasks` | Task 1: register then login with real handlers, the token's `sub` is the new user's id. Task 3: if the auto sign-in fails, send the user to `/login` with "Account created. Please sign in." |
| Tasks are private to their owner | `ListByUserAsync_returns_only_that_users_tasks`, `GetAsync_with_other_users_id_returns_null`, `UpdateAsync_sameIdUnderOtherUser_…`, `DeleteAsync_sameIdUnderOtherUser_…`, `GetById_for_other_users_task_returns_404`, `Create_ignores_userId_in_body` | Nothing. Already pinned at the repository, handler and API levels |

## Decisions

- **D36: no new backend production code.** Registration, unique email and the password policy already meet the criteria. The backend gets tests only.
- **D37: one password copy everywhere.** The client shows the server's exact sentences: "Password must be at least 8 characters.", "Password must be at most 128 characters.", "Password must contain at least one letter and one digit."
- **D38: show the password rule up front.** A `mat-hint` under the password field reads "At least 8 characters, with a letter and a digit." It is hidden while an error shows, which is Material's default behavior.
- **D39: account created but sign-in failed → `/login?reason=registered`.** `AuthStore.register` returns `'signed-in' | 'registered'` and never throws once the account exists. The Login page shows "Account created. Please sign in." for `reason=registered`, using the `reason` input that `reason=expired` already uses. The email is not put in the URL.
- **D40: no email verification, password strength meter or extra rules (YAGNI).**

## Global Constraints

- Clean Architecture and CQRS rules from `.claude/rules/rules.md`. NetArchTest stays green.
- Never log passwords, hashes or tokens, and that includes test output.
- Tests are named `Method_condition_expected` (backend) and `'<Class>: <behavior>'` (web, matching the existing specs).
- `dotnet build -warnaserror` is clean. Domain and Application coverage stays ≥ 80%. `ng lint --max-warnings 0` passes, with zero console warnings.
- Conventional Commits, each ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- UI copy, exact: "At least 8 characters, with a letter and a digit.", "Password must contain at least one letter and one digit.", "Account created. Please sign in."

## Review Focus

1. **Duplicate email that differs only by case or spaces** (`" Ana@X.com "` after `ana@x.com`): `409`, and no second user is written. Pinned in Task 1.
2. **Registration succeeds but the automatic login fails** (login rate limit or a 5xx): the user is not left on the register form. A retry there would get a confusing "already exists". Pinned in Task 3.
3. **Weak password typed and submitted**: the reason shows under the field and no request is sent. Pinned in Task 2.
4. **The server rejects a password the client accepted** (the rules drift apart later): the server's message shows under the password field. Pinned in Task 2.
5. **A brand-new account's token works on the Tasks API**: the Tasks API trusts the same `sub`/issuer/audience that `JwtTokenIssuer` issues. Already pinned by `JwtTokenIssuerTests` and `TestJwt` (the same claims and defaults). Reviewer check only.

---

### Task 1: Backend key flow — sign up, then sign in

**Files:**
- Test: `tests/TaskManager.Users.Api.Tests/SignUpFlowTests.cs` (new)

**Interfaces:**
- Consumes: `new UsersApiFactory(useRealHandlers: true)` and its substituted `IUserRepository Repository`. The real `AspNetPasswordHasher` and `JwtTokenIssuer` stay registered.

These tests describe behavior that already exists, so they are characterization tests and are expected to pass on the first run. To prove each one can fail, break it on purpose once (for example, change the expected status) and see it go red, then revert.

- [ ] **Step 1: Write the tests.** Back `Repository` with a dictionary keyed by `Email.Value`. `TryAddAsync` returns `AddUserResult.EmailTaken` when the key exists, otherwise it stores the user and returns `Added`. `GetByEmailAsync` reads from the dictionary.

```csharp
[Fact]
public async Task RegisterThenLogin_newAccount_returnsTokenForTheNewUser()
// POST /api/v1/auth/register { "Ana Lopez", "ana@x.com", "Passw0rd!" } → 201, read body.id
// POST /api/v1/auth/login    { "ana@x.com", "Passw0rd!" }                → 200
// new JsonWebTokenHandler().ReadJsonWebToken(body.accessToken).Subject.ShouldBe(id)

[Fact]
public async Task Register_sameEmailDifferentCaseAndSpaces_returns409AndStoresOneUser()
// register "ana@x.com" → 201; register " Ana@X.COM " → 409 with code "users.email_taken"
// store.Count.ShouldBe(1)
```

- [ ] **Step 2: Run them.** `dotnet test tests/TaskManager.Users.Api.Tests --filter SignUpFlowTests`. Expected: 2 passed. Then do the deliberate-break check above.

- [ ] **Step 3: Commit.** `test(users): pin sign-up then sign-in and case-insensitive unique email`

---

### Task 2: Web — tell the user when the password is too weak

**Files:**
- Modify: `web/src/app/features/auth/register/register.html` (password `mat-form-field`)
- Test: `web/src/app/features/auth/register/register.spec.ts`

- [ ] **Step 1: Write the failing tests** (use the existing `setup`, `type`, `q` and `submit` helpers)

```ts
it('Register: weak password shows the rule and does not submit', () => {
  // fill(el), then type password and confirm as 'password' (no digit), click submit, detectChanges
  expect(el.textContent).toContain('Password must contain at least one letter and one digit.');
  expect(store.register).not.toHaveBeenCalled();
});

it('Register: shows the password hint before anything is typed', () => {
  expect(el.textContent).toContain('At least 8 characters, with a letter and a digit.');
});

it('Register: server password errors show under the password field', async () => {
  // reject with { status: 400, message: 'One or more validation errors occurred.',
  //   fieldErrors: { password: ['Password must be at least 8 characters.'] } }
  expect(el.textContent).toContain('Password must be at least 8 characters.');
  expect(el.querySelector('[role=alert]')).toBeNull();
});
```

- [ ] **Step 2: Run them.** `npx ng test --watch=false --include src/app/features/auth/register`. Expected: the first two FAIL. The third already passes, because `showServerError` maps field errors. Keep it as a pin.

- [ ] **Step 3: Implement.** In `register.html`, add `<mat-hint>At least 8 characters, with a letter and a digit.</mat-hint>` to the password field (import `MatHint` in `register.ts`). Change the `pattern` error text to "Password must contain at least one letter and one digit." (D37).

- [ ] **Step 4: Run** the register specs and `npx ng lint --max-warnings 0`. Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit.** `feat(web): show the password rule and match the server's weak-password copy`

---

### Task 3: Web — account created but automatic sign-in failed

**Files:**
- Modify: `web/src/app/core/auth/auth-store.ts` (`register`)
- Modify: `web/src/app/features/auth/register/register.ts` (`submit`)
- Modify: `web/src/app/features/auth/login/login.html` (next to the `reason() === 'expired'` block)
- Test: `web/src/app/core/auth/auth-store.spec.ts`, `web/src/app/features/auth/register/register.spec.ts`, `web/src/app/features/auth/login/login.spec.ts`

**Interfaces:**
- Produces: `AuthStore.register(req: RegisterRequest): Promise<'signed-in' | 'registered'>`. It rejects only when the register call itself fails, and resolves to `'registered'` when the login call fails afterwards.

- [ ] **Step 1: Write the failing tests**

```ts
// auth-store.spec.ts. Update the existing register test to expect resolve 'signed-in'. Add:
it('register resolves "registered" and stays signed out when login fails after sign-up', async () => {
  api.register.mockReturnValue(of(user));
  api.login.mockReturnValue(throwError(() => ({ status: 503, message: 'busy' })));
  await expect(store.register(req)).resolves.toBe('registered');
  expect(store.isAuthenticated()).toBe(false);
});

// register.spec.ts. Change the success test's mock to mockResolvedValue('signed-in'). Add:
it('Register: account created but not signed in navigates to /login?reason=registered', async () => {
  store.register.mockResolvedValue('registered');
  // fill + submit + whenStable
  expect(navigate).toHaveBeenCalledWith('/login?reason=registered');
});

// login.spec.ts
it('Login: reason=registered shows "Account created. Please sign in."', () => {
  // fixture.componentRef.setInput('reason', 'registered'); detectChanges
  expect(el.textContent).toContain('Account created. Please sign in.');
});
```

- [ ] **Step 2: Run them.** `npx ng test --watch=false --include src/app/core/auth --include src/app/features/auth`. Expected: the 3 new tests FAIL.

- [ ] **Step 3: Implement.** `AuthStore.register` wraps only the `login` call in try/catch and returns `'registered'` from the catch. `Register.submit` navigates to `'/tasks'` for `'signed-in'` and to `'/login?reason=registered'` otherwise. `login.html` adds an `@else if (reason() === 'registered')` branch with the exact copy, styled like the existing `expired` notice. Update the `vi.fn` type in `register.spec.ts` to `Promise<'signed-in' | 'registered'>`.

- [ ] **Step 4: Run** `npx ng test --watch=false` and `npx ng lint --max-warnings 0`. Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit.** `fix(web): send the user to sign in when sign-up succeeds but auto sign-in fails`

---

### Task 4: Verify and update the README status

- [ ] **Step 1:** Run `dotnet build -warnaserror`, then `dotnet test` (DynamoDB Local running). Check that coverage stays ≥ 80%, then run `npx ng test --watch=false` and `npx ng lint --max-warnings 0`. All must be green.
- [ ] **Step 2:** In `README.md` "Project status", change the Story 6 (#20) row to "In progress on `feature/story-6-create-my-own-account`", split it out of "Stories 6-9", and set "Last updated".
- [ ] **Step 3: Commit.** `docs: add Story 6 plan and mark it in progress`
