# F2 — Trips: Implementation plan

_Status: Approved · Last updated: 2026-10-22 · Spec: [F2-trips](../specs/features/F2-trips.md) (Approved, last updated 2026-10-21)_

## Overview
F2 adds trips: create a trip with a name, list your own trips newest first, open a trip to a placeholder page, and delete it after a confirmation. It adds the `trips` table and four owner-checked endpoints, and replaces F1's post-login placeholder with the trip list. It is the second build step (F1 → F6). F3–F6 attach people, expenses and payments to a trip, so F2 also fixes the convention they follow so that deleting a trip removes everything in it.

## Prerequisites
- **Dependencies:**
  - **F1: planned, not built (blocking).** `app/` is empty. F2 cannot start until at least F1.T1–T10 and T13 exist. F2 reuses from F1: the `users` table, `IDbConnectionFactory` and `IUnitOfWork`, `ICurrentUser`, the session authentication handler and the default-deny authorization policy, the ProblemDetails middleware, `FakeTimeProvider` usage, and on the front end `AuthService`, the auth guard, the HTTP interceptor and `problem-mapper.ts`. The names above are the ones in the F1 plan. If F1 is built under different names, adjust F2's task text, not the intent.
  - F3–F6: not started. F2 does not depend on them. They depend on F2.
- **Other features' code:** F2.T5 changes code F1 owns: the post-login placeholder (F1.T13) and the F1 tests that assert it. See that task.
- **Setup:** none beyond F1's (Docker for the local PostgreSQL). F2's own tests need no database.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | `TripExpenseTracker.Data/Migrations/0002_create_trips.sql` | `trips` table and index |
| `TripExpenseTracker.Core` | `Trips/` (`Trip`, `TripNameRules`, `TripLimits`, `ITripDao`, `TripService`) | Name rules, 50-trip limit, create/list/get/delete orchestration |
| `TripExpenseTracker.Data` | `TripDao.cs` | Dapper queries, owner in every `WHERE` |
| `TripExpenseTracker.Api` | `Controllers/TripsController.cs`, request and response records | Four endpoints under `/api/trips` |
| Angular (`app/src/web/src/app/trips/`) | `trips.service.ts`, `trip-list/`, `trip-create-form/`, `trip-page/`, `delete-trip-dialog/`, routes | List, create, trip page, delete dialog. Replaces the F1 placeholder. |
| Tests | `TripExpenseTracker.Tests`, web specs | Unit tests only (project rules): Core and controllers with fakes, Angular components with mocked HTTP. Database behaviour is checked in code review. |

Rules from `CLAUDE.md` and `project-rules.md` that are easy to break here:
- **Ownership in the query.** Every `TripDao` query has `owner_user_id = @owner` in its `WHERE` clause. Never fetch by id alone and compare in code. A foreign trip and a missing trip must look identical (F2.R9).
- **Parameterized SQL only.**
- **Race on the 50-trip limit.** Count and insert happen in one transaction that first locks the owner's `users` row (see Contract changes). Checking only in the form or only before the insert is not enough (F2.R4, Edge cases).
- **Time from `TimeProvider`**, stored in UTC. Tests never read the real clock.
- **Logging.** `ILogger` only. Do not log trip names at information level together with user ids beyond what is needed. Never log session IDs or cookies.
- **Copy** comes exactly from the spec's States & errors table.

## Contract changes
- **Database** (`0002_create_trips.sql`):
  ```sql
  CREATE TABLE trips (
    id            uuid PRIMARY KEY,
    owner_user_id uuid NOT NULL REFERENCES users(id),
    name          text NOT NULL CHECK (char_length(name) BETWEEN 1 AND 100),
    created_at    timestamptz NOT NULL      -- set by the app from TimeProvider
  );
  CREATE INDEX ix_trips_owner_created ON trips (owner_user_id, created_at DESC, id DESC);
  ```
  `char_length` counts characters (code points), the same unit F1 uses for passwords. The app trims before saving, so the check guards the length only.
- **Cascade convention for F3–F6 (F2.R12).** Deleting a trip is one `DELETE FROM trips WHERE id = @id AND owner_user_id = @owner`. Every later table that belongs to a trip declares `trip_id uuid NOT NULL REFERENCES trips(id) ON DELETE CASCADE`, so one statement removes everything atomically. References **between** child tables, for example an expense pointing at a person, must use the default `NO ACTION`, not `RESTRICT`. `NO ACTION` is checked at the end of the statement, so the trip cascade can delete the people and the expenses together. `RESTRICT` fails at once and would block the cascade. F3.R7 and R7 in product.md still hold, because a single-person delete outside a trip delete is refused by the same constraint.
- **API** (all need a session through F1's default policy; owner comes from `ICurrentUser`):
  | Method and path | Request | Success | Failure |
  |---|---|---|---|
  | `GET /api/trips` | none | `200` `[{ id, name, createdAt }]`, newest first (`created_at DESC, id DESC`), at most 50 items so no paging | `401` |
  | `POST /api/trips` | `{ name, confirmDuplicate? }` | `201` `{ id, name, createdAt }` with `Location` | `400` validation with codes `name_required`, `name_too_long`. `409` ProblemDetails `type` `duplicate-trip-name` when a same-name trip exists and `confirmDuplicate` is not `true`. `409` ProblemDetails `type` `trip-limit-reached` at 50 trips. `401`. |
  | `GET /api/trips/{id:guid}` | none | `200` `{ id, name, createdAt, peopleCount, expenseCount, paymentCount }` | `404` ProblemDetails (not owned, missing, or not a GUID: identical). `401` |
  | `DELETE /api/trips/{id:guid}` | none | `204` | `404` as above. `401` |
  - The three counts are `0` in F2. F3, F4 and F6 each make their count real in their own plans, so the delete dialog needs no change later (F2.R7).
  - No `PUT` or `PATCH` exists (F2.R8).
  - The duplicate check ignores capitals and runs in SQL (`lower(name) = lower(@name)`) on the user's own trips, using the trimmed name. It is advisory: two simultaneous requests may both pass it, which is allowed because duplicates are permitted after confirmation.
  - Create runs in one unit of work: `SELECT id FROM users WHERE id = @owner FOR UPDATE`, then count, then the duplicate check, then insert. The lock serializes a user's own creates, so two requests at 49 trips yield at most one new trip. Locking `users` also settles it for deletes, since a delete only lowers the count.
- **Browser storage:** none. The "Trip deleted." message travels in router navigation state and is not stored.

## Tasks

### F2.T1 — `trips` migration
- **Files:** `app/src/api/TripExpenseTracker.Data/Migrations/0002_create_trips.sql` (embedded resource).
- **Change:** add the table and index from Contract changes. No child tables and no seed data.
- **Covers:** F2.R4 (stored name length), F2.R5 (ordering index), F2.R9 (owner column), F2.R12 (convention starts here).
- **Tests:** none automated (unit tests only). **Review:** the length check, the `owner_user_id` foreign key and the index match Contract changes, and the script only adds.
- **Done when:** the API starts in Development and DbUp applies `0002` to the local database without errors, and a reviewer has read the SQL.
- _Needs human review: database migration._

### F2.T2 — Core: trip rules, limit and `TripService`
- **Files:** `TripExpenseTracker.Core/Trips/Trip.cs`, `TripNameRules.cs`, `TripLimits.cs`, `ITripDao.cs`, `TripService.cs`, `TripResults.cs`.
- **Change:** pure logic and interfaces, no I/O.
  - `TripNameRules.Normalize` trims the ends. A blank result gives `name_required`. More than 100 code points gives `name_too_long`. Any characters are allowed (F2.R2).
  - `TripLimits` holds `MaxTripsPerUser = 50` and `MaxNameLength = 100` as constants, because the spec fixes them.
  - `ITripDao`: `ListAsync(owner)`, `GetAsync(owner, id)`, `CountAsync(owner)`, `ExistsWithNameAsync(owner, name)`, `InsertAsync(trip)`, `DeleteAsync(owner, id)` (returns whether a row was removed), plus `LockOwnerAsync(owner)` used inside the unit of work.
  - `TripService.CreateAsync` order: validate → open unit of work → lock owner → refuse at 50 → refuse a duplicate unless confirmed → insert with `TimeProvider.GetUtcNow()` and a new id → commit. Get and delete return "not found" when the DAO returns nothing (F2.R9).
- **Covers:** F2.R1, F2.R2, F2.R3 (flow), F2.R4, F2.R9, F2.AC2, F2.AC3, F2.AC5 (logic).
- **Tests:** unit (xUnit, FluentAssertions, fake DAO, `FakeTimeProvider`): `"  Paris 2026  "` is saved as `Paris 2026`; only spaces gives `name_required`; 100 characters passes and 101 gives `name_too_long`; 100 emoji pass and 101 fail; at 49 trips a create succeeds, at 50 it returns `trip-limit-reached` and inserts nothing, and a count of 51 also refuses; a duplicate without confirmation returns the duplicate result and inserts nothing; with confirmation it inserts; the limit is checked before the duplicate; `createdAt` equals the fake clock.
- **Done when:** the tests pass and `Core` has no reference to `Data`, Npgsql or ASP.NET.

### F2.T3 — Data: `TripDao`
- **Files:** `TripExpenseTracker.Data/TripDao.cs`, DI registration.
- **Change:** implement `ITripDao` with Dapper. Every query includes `owner_user_id = @owner`. `ListAsync` orders by `created_at DESC, id DESC`. `ExistsWithNameAsync` uses `lower(name) = lower(@name)`. `DeleteAsync` is a single `DELETE ... WHERE id = @id AND owner_user_id = @owner` that reports the affected row count. `LockOwnerAsync` runs `SELECT id FROM users WHERE id = @owner FOR UPDATE` on the unit of work's transaction.
- **Covers:** F2.R3, F2.R4, F2.R5, F2.R9, F2.R12, F2.AC4 (case rule), F2.AC5, F2.AC6, F2.AC7.
- **Tests:** none automated (unit tests only; the DAO is SQL). **Code review checklist:**
  - every query has `owner_user_id = @owner` in its `WHERE` (the lock uses the owner id), so B can never read or delete A's trip (F2.AC6, F2.R9);
  - the list orders by `created_at DESC, id DESC` (F2.AC7);
  - the duplicate check uses `lower(name) = lower(@name)` on the same owner's trips only (F2.AC4);
  - `LockOwnerAsync` runs `FOR UPDATE` on the unit of work's transaction, before the count and insert, so parallel creates at 49 cannot reach 51 (F2.R4);
  - delete is one `DELETE ... WHERE id = @id AND owner_user_id = @owner` that returns the row count (F2.R12);
  - parameters only, no string-concatenated SQL.
- **Done when:** the build passes, the checklist is ticked by a reviewer, and a manual run (create, list, delete as two users) behaves as above.
- _Needs human review: ownership in SQL (no automated test covers it)._

### F2.T4 — Api: `TripsController`
- **Files:** `TripExpenseTracker.Api/Controllers/TripsController.cs`, `Trips/CreateTripRequest.cs`, `TripResponse.cs`, `TripDetailResponse.cs`; mapping of service results to status codes; `TripExpenseTracker.Tests/Api/TripsControllerTests.cs`.
- **Change:** the four endpoints in Contract changes. They rely on F1's default-deny policy, with no `[AllowAnonymous]`. The owner id comes only from `ICurrentUser`, never from the request. Results map to the codes in Contract changes. The detail response returns the three counts as `0`. A non-GUID id returns the same `404` as a missing trip.
- **Covers:** F2.R1–R9, F2.AC1–AC7, F2.AC10 (server side), F2.AC11, F2.AC15 (API half: `401` when logged out).
- **Tests:** unit (xUnit, the controller built directly with a fake `TripService` result source and a fake `ICurrentUser`, no web host or database):
  - a created trip returns `201` with `Location` and the body shape (F2.AC1);
  - `name_required` and `name_too_long` results map to `400` with those codes (F2.AC2, F2.AC3);
  - the duplicate result maps to `409` `duplicate-trip-name`, and `confirmDuplicate: true` is passed through to the service (F2.AC4);
  - the limit result maps to `409` `trip-limit-reached` (F2.AC5);
  - "not found" maps to the same `404` ProblemDetails as an id that isn't a GUID (F2.AC6, F2.AC11, F2.R9);
  - the detail response has the name and three zero counts (F2.AC8, API half); delete returns `204` (F2.AC10);
  - the owner id passed to the service is always the fake `ICurrentUser`'s, never a value from the request.
- **Review:** no `[AllowAnonymous]` on the controller, so F1's default-deny policy returns `401` when logged out (F2.AC15); no `PUT` or `PATCH` actions exist (F2.R8).
- **Done when:** the tests pass and no endpoint under `/api/trips` accepts an owner id from the client.
- _Needs human review: authorization and ownership checks._

### F2.T5 — Replace the F1 placeholder with the trip list page (touches F1 code)
- **Files:** `app/src/web/src/app/trips/trip-list/trip-list.component.{ts,html,scss}`, `trips.service.ts`, `app.routes.ts`; **F1 files changed:** `trips-placeholder.component.*` (removed), F1.T13 front-end specs and F1.T15 e2e specs that assert the placeholder.
- **Change:**
  - `/trips` now loads the list page. Shows "Loading your trips…" while fetching. Shows the empty state "You have no trips yet. Create your first trip to start tracking shared expenses." when the list is empty (F2.R11). Otherwise a real `<ul>` of trips newest first, each trip name a link to `/trips/{id}` (F2.R5).
  - The page heading is "Your trips" (mockup `F2 · Your trips.png`). The log-out button stays in the shared header from F1.T13.
  - The guard from F1 protects the route. A logged-out visitor is redirected with "Please log in to see your trips." (F1.R15, F2.AC15).
  - After login and after registration the user lands here (F2.R10, F2.AC14).
  - The F1 specs that assert the placeholder text "You're logged in. Your trips will appear here." are replaced by specs that assert the trip list. This is a spec-driven change: F2.R10 states that the list replaces F1.R16. F1's existing e2e steps that end on the placeholder are updated to end on the trip list, because existing tests must keep passing (project rules). No new e2e test is added.
- **Covers:** F2.R5, F2.R10, F2.R11, F2.AC12, F2.AC14, F2.AC15.
- **Tests:** front end (Angular Testing Library, mocked HTTP): the loading text shows first; an empty list shows the empty state and no list; three trips render in the order the API returns them as a `list` with three `listitem`s; each trip name is a link to its page; after a successful login the router ends on the list; a logged-out `/trips` redirects to `/login` with the message. Updated F1 specs pass with the new assertions.
- **Done when:** the tests pass, no reference to the placeholder component remains, and a reviewer has confirmed that each changed F1 assertion matches F2.R10 and no F1 test was deleted, skipped or weakened beyond that.
- _Needs human review: changes and replaces existing F1 tests (project rules)._

### F2.T6 — Create form, duplicate warning and limit message
- **Files:** `trips/trip-create-form/trip-create-form.component.{ts,html,scss}`, spec; `trip-list` wiring; `problem-mapper.ts` additions.
- **Change:** a Reactive Form on the trip list page with a visible label "New trip" and the **Create trip** button (mockup). It shows the empty-state copy together with the form (F2.R11).
  - Client checks mirror F2.R2: "Enter a trip name." and "A trip name can be at most 100 characters." The server codes `name_required` and `name_too_long` map to the same copy.
  - On `409` `duplicate-trip-name`, show above the form: "You already have a trip called '{name}'. Create another one with the same name?" with an icon and the buttons **Create anyway** and **Cancel**. **Create anyway** resends with `confirmDuplicate: true`. **Cancel** hides the warning, creates nothing and leaves the typed text unchanged.
  - When the list has 50 trips, or the server returns `409` `trip-limit-reached`, the form is disabled and shows "You have reached the limit of 50 trips. Delete a trip to create a new one." with an icon. On the server response the list is reloaded.
  - While sending, the button is disabled and shows "Creating trip…". On success the trip appears at the top of the list, the field clears and no message is shown. A network or server error shows "Something went wrong. Please try again." above the form.
  - Errors, the warning and the limit message are linked to the field with `aria-describedby` and announced (live region or `role="alert"`).
- **Covers:** F2.R1, F2.R2, F2.R3, F2.R4, F2.R11, F2.AC1–AC5; spec, Accessibility.
- **Tests:** front end: both field errors show with the exact copy and no request is sent; 100 characters sends, 101 does not; the duplicate warning shows the typed name; Cancel sends no request; Create anyway sends `confirmDuplicate: true` and the new trip appears first; with 50 trips in the list the form is disabled and the limit text is shown; a limit `409` disables the form; the button is disabled during the request; the field clears on success; the server error text shows on a `500`; the form works with the keyboard alone and the warning is announced.
- **Done when:** the tests pass and all visible text matches the spec's States & errors table word for word.

### F2.T7 — Trip page and delete dialog
- **Files:** `trips/trip-page/trip-page.component.{ts,html,scss}`, `trips/delete-trip-dialog/delete-trip-dialog.component.{ts,html,scss}`, specs, `app.routes.ts` (`/trips/:tripId`).
- **Change:**
  - The guarded trip page loads `GET /api/trips/{id}` and shows the trip name as a heading and a **Delete** button whose accessible name is "Delete trip {name}" (F2.R6). It has no rename or edit control (F2.R8).
  - On `404` (not owned, missing, or deleted in another tab) it shows "This trip no longer exists." and a link back to the list. The link text is **"Your trips"**, reusing the list heading, because the spec gives no link copy (see Risks).
  - **Delete** opens an Angular CDK dialog with `role="dialog"` and labelled title and body. Title: "Delete {name}?" Body: "This deletes the trip and everything in it. You can't undo this." Counts from the detail response: "{people} people", "{expenses} expenses", "{payments} payment". Buttons: **Delete trip** and **Cancel** (mockup `F2 · Delete trip confirmation.png`). The dialog traps focus, **Escape** closes it, and focus returns to the Delete button that opened it. Initial focus goes to **Cancel**, since the action is destructive.
  - **Cancel** closes the dialog and leaves the trip. **Delete trip** calls `DELETE`. On `204` it navigates to `/trips` with "Trip deleted." in navigation state, shown on the list in a `role="status"` region. On `404` it shows "This trip no longer exists." On any other error it shows "Something went wrong. Please try again." inside the dialog.
  - Buttons and links are at least 24×24 CSS pixels.
- **Covers:** F2.R6, F2.R7, F2.R8, F2.R9 (front-end side), F2.AC6 (message), F2.AC8–AC11, F2.AC13; spec, Accessibility.
- **Tests:** front end: the page shows the name and the Delete button with the accessible name "Delete trip Paris 2026" (F2.AC8); there is no edit control (F2.AC13); a `404` shows "This trip no longer exists." and the link (F2.AC6, F2.AC11); the dialog shows the title, body and the three counts as `0 people`, `0 expenses`, `0 payment`; Cancel closes it and no `DELETE` is sent (F2.AC9); Escape closes it and focus returns to the Delete button; focus stays inside while open; confirming sends `DELETE`, navigates to the list and "Trip deleted." appears (F2.AC10); a `404` on delete shows the not-found message; a `500` shows the error inside the dialog.
- **Done when:** the tests pass and the dialog copy matches the spec exactly.

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F2.R1 | T2, T4, T6 | AC1 | T4 `201` mapping; T6 form submit |
| F2.R2 | T1, T2, T4, T6 | AC2, AC3 | T2 trim and length unit tests; T4 `400` codes; T6 field errors |
| F2.R3 | T2, T3, T4, T6 | AC4 | T2 duplicate flow with a fake DAO; T3 review (case rule in SQL); T4 `409` mapping; T6 warning and buttons |
| F2.R4 | T2, T3, T4, T6 | AC5 | T2 count at 49/50/51; T3 review (row lock against races); T4 `409` mapping; T6 limit message |
| F2.R5 | T1, T3, T5 | AC7 | T3 review (`ORDER BY`); T5 renders the order the API returns |
| F2.R6 | T4, T7 | AC8 | T4 detail response; T7 trip page and Delete button |
| F2.R7 | T4, T7 | AC9, AC10 | T7 dialog copy, counts, Cancel and confirm; T4 `204` |
| F2.R8 | T4, T7 | AC13 | T4 review (no `PUT`/`PATCH`); T7 no edit control |
| F2.R9 | T3, T4, T7 | AC6 | T3 review (owner in every `WHERE`); T4 identical `404` and owner from `ICurrentUser`; T7 not-found page |
| F2.R10 | T5 | AC14 | T5 login lands on the list |
| F2.R11 | T5, T6 | AC12 | T5 empty state; T6 empty state with form |
| F2.R12 | T1, T3 | AC10 | T3 review (single owner-scoped `DELETE`; children cascade per Contract changes). F3–F6 reviews check their own `ON DELETE CASCADE`. |

| Acceptance criterion | Tasks | Test |
|---|---|---|
| F2.AC1 | T4, T6 | T4 `201`; T6 new trip shown first |
| F2.AC2 | T2, T4, T6 | T2 spaces only; T4 `400`; T6 "Enter a trip name." |
| F2.AC3 | T2, T4, T6 | T2 at 101 and 100 characters; T4 `400`; T6 |
| F2.AC4 | T2, T3, T4, T6 | T2 duplicate flow; T3 review; T4 `409`; T6 Cancel and Create anyway |
| F2.AC5 | T2, T3, T4, T6 | T2 limit at 50; T3 review; T4 `409`; T6 limit message |
| F2.AC6 | T3, T4, T7 | T3 review; T4 same `404` as a missing id; T7 message; manual check with two users |
| F2.AC7 | T3, T5 | T3 review; T5 list order |
| F2.AC8 | T4, T7 | T7 name and Delete button |
| F2.AC9 | T7 | T7 Cancel sends no `DELETE` |
| F2.AC10 | T4, T7 | T4 `204`; T7 "Trip deleted." |
| F2.AC11 | T4, T7 | T4 `404` mapping; T7 not-found copy |
| F2.AC12 | T5 | T5 empty state with form |
| F2.AC13 | T4, T7 | T7 no edit control |
| F2.AC14 | T5 | T5 router lands on the list |
| F2.AC15 | T4, T5 | T4 review (default-deny, no `[AllowAnonymous]`); T5 redirect with message |

## Test plan
Unit tests only (project rules).
- **Unit (xUnit, Core):** name trimming, blank check and the 100-character limit; the count check at 49, 50 and 51; duplicate and limit order in `TripService` (T2).
- **Unit (xUnit, Api):** controller result-to-status mapping, the identical `404`, and the owner always taken from `ICurrentUser` (T4).
- **Front end (Angular Testing Library, mocked HTTP):** list, empty and loading states, login landing (T5); create form, duplicate warning, limit (T6); trip page, not-found and delete dialog with focus checks (T7).
- **Code review (not testable with unit tests):** T1 migration SQL; T3 checklist (owner in every `WHERE`, case-insensitive duplicate check, `ORDER BY`, the owner row lock against the 50-trip race, the single owner-scoped `DELETE`).
- **Existing suites:** F1's integration and Playwright tests must still pass after T5 updates the placeholder assertions. Run them once at the end of F2.
- **Worked examples:** none. The product.md examples belong to F4–F6.
- **Manual checks:** phone-width layout of the list, form and dialog; keyboard-only through create, duplicate warning, open and delete; a screen reader reading the list item count, the "Delete trip {name}" buttons and the warning; 4.5:1 contrast on text and warnings; visible focus; focus trapped in the dialog and returned on close; the warning and limit message are readable without colour.

## Risks & mitigations
- **No automated test covers the SQL.** Ownership, the case-insensitive duplicate check, ordering, the race-proof limit and the delete are verified only by the T3 review and a manual run with two users. A missed `owner_user_id` would leak trips between users, so T3 is marked for human review.
- **F1 is not built (blocker).** F2 starts only after F1.T1–T10 and T13 are done. If F1's names or behavior change while it is built, update F2's tasks. The F2 plan assumes F1's default-deny authorization, `ICurrentUser` and the unit of work.
- **Spec wording conflict about the card Delete action.** Scope and the mockup bullet in User flow say cards show "its Delete action", while the 2026-10-21 decision and F2.R6 and R7 put Delete only on the trip page. The plan follows the decision: cards show the trip name only, as a link, and no card menu. It suggests a one-line spec fix in `/spec-feature F2`, which would put the spec back to Draft. The plan does not need it to proceed.
- **Copy the spec does not give.** The not-found page needs a link label, and the plan uses "Your trips" (the list heading). The mockup's "Open trip →" button, the "3 trips · 1 still to settle" subtitle and the card "..." menu are not in the spec, so they are not built. The spec's literal "{payments} payment" is used with a singular label even for 0 or 2. Add copy to the spec if you want any of these.
- **F1 tests change in T5.** The placeholder assertions are replaced because F2.R10 says so. T5 is marked for human review for that reason. No F1 test is deleted or skipped.
- **Cross-feature work for F3–F6.** Their plans must (a) declare `trip_id ... ON DELETE CASCADE` and use `NO ACTION` for references between child tables (Contract changes), (b) include that in their migration review, and (c) make `peopleCount`, `expenseCount` and `paymentCount` in the trip detail real. F3–F6 are not planned yet, so this is recorded here.
- **Duplicate check is advisory.** Two simultaneous creates of the same new name can both pass the check, which is fine because duplicates are allowed after confirmation. Only the 50-trip limit is made race-proof.
- **Order for identical timestamps.** The spec allows either order. The plan adds `id DESC` as a tiebreak so the order is stable, and the tests rely on that.
- **Open questions in the spec:** none.

## Out of scope
- Editing a trip (F2.R8), other trip fields, undo after delete, and sharing trips (spec, Out).
- People, expenses, balances, payments and the real counts in the delete dialog (F3–F6).
- Totals, people and expense counts and settle status on trip cards, the card "..." menu, and an "Open trip" button (spec, Out and Risks above).
- Returning to a previously requested page after login (F1.R15).

## Decisions log
- 2026-10-21: _(Proposed in the plan, not asked; no product behavior changes.)_ Paths `GET/POST /api/trips` and `GET/DELETE /api/trips/{id}`. A duplicate warning is a `409` with `type` `duplicate-trip-name` and the client confirms with `confirmDuplicate: true`. The limit is a `409` `trip-limit-reached`. Validation returns the codes `name_required` and `name_too_long`, and the front end owns the copy (same pattern as F1). The trip detail returns the three counts, `0` in F2, so the dialog needs no change when F3–F6 land. `id` is a `uuid`. No paging, because the limit is 50.
- 2026-10-21: _(Proposed.)_ The 50-trip limit is enforced by locking the owner's `users` row in the create transaction, with count and insert after it, rather than a trigger or an advisory lock.
- 2026-10-21: _(Proposed.)_ Deleting a trip is one `DELETE` that relies on `ON DELETE CASCADE` from every child table (F2.R12). References between child tables use `NO ACTION` so the cascade is not blocked.
- 2026-10-21: _(Proposed.)_ Delete is offered only on the trip page, following the spec decision of 2026-10-21. Initial focus in the delete dialog is on **Cancel**.
- 2026-10-22: Test scope reduced to unit tests only, following the project rules change. T1 and T3 lose their integration tests and get code-review checklists instead; T4's endpoint tests become controller unit tests with fakes; F2.T8 (Playwright end-to-end) is removed and its ID is not reused. F1's existing integration and e2e tests stay and must pass. Status set back to Draft for re-approval.
- 2026-10-22: Approve the plan with unit tests only? → Yes. Status set to Approved.
