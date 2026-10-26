# F2 — Trips: Implementation plan

_Status: Approved · Last updated: 2026-10-21 · Spec: [F2-trips](../specs/features/F2-trips.md) (Approved, last updated 2026-10-21)_

## Overview
F2 adds trips: create a trip with a name, list your own trips newest first, open a trip to a placeholder page, and delete it after a confirmation. It adds the `trips` table and four owner-checked endpoints, and replaces F1's post-login placeholder with the trip list. It is the second build step (F1 → F6). F3–F6 attach people, expenses and payments to a trip, so F2 also fixes the convention they follow so that deleting a trip removes everything in it.

## Prerequisites
- **Dependencies:**
  - **F1: planned, not built (blocking).** `app/` is empty. F2 cannot start until at least F1.T1–T10 and T13 exist. F2 reuses from F1: the `users` table, `IDbConnectionFactory` and `IUnitOfWork`, `PostgresFixture` (Testcontainers), `ICurrentUser`, the session authentication handler and the default-deny authorization policy, the ProblemDetails middleware, `FakeTimeProvider` usage, and on the front end `AuthService`, the auth guard, the HTTP interceptor and `problem-mapper.ts`. The names above are the ones in the F1 plan. If F1 is built under different names, adjust F2's task text, not the intent.
  - F3–F6: not started. F2 does not depend on them. They depend on F2.
- **Other features' code:** F2.T5 changes code F1 owns: the post-login placeholder (F1.T13) and the F1 tests that assert it. See that task.
- **Setup:** none beyond F1's (Docker for PostgreSQL and Testcontainers).

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | `TripExpenseTracker.Data/Migrations/0002_create_trips.sql` | `trips` table and index |
| `TripExpenseTracker.Core` | `Trips/` (`Trip`, `TripNameRules`, `TripLimits`, `ITripDao`, `TripService`) | Name rules, 50-trip limit, create/list/get/delete orchestration |
| `TripExpenseTracker.Data` | `TripDao.cs` | Dapper queries, owner in every `WHERE` |
| `TripExpenseTracker.Api` | `Controllers/TripsController.cs`, request and response records | Four endpoints under `/api/trips` |
| Angular (`app/src/web/src/app/trips/`) | `trips.service.ts`, `trip-list/`, `trip-create-form/`, `trip-page/`, `delete-trip-dialog/`, routes | List, create, trip page, delete dialog. Replaces the F1 placeholder. |
| Tests | `TripExpenseTracker.Tests`, `TripExpenseTracker.IntegrationTests`, web specs, `app/e2e` | Unit, integration, component and Playwright |

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
- **Tests:** integration (Testcontainers): the migration runs on top of `0001`. A name of 0 or 101 characters is rejected by the check. A row with a non-existent `owner_user_id` is rejected. Running all migrations twice changes nothing.
- **Done when:** the tests pass against a fresh container.
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
- **Files:** `TripExpenseTracker.Data/TripDao.cs`, DI registration; integration tests in `IntegrationTests/Data/TripDaoTests.cs`.
- **Change:** implement `ITripDao` with Dapper. Every query includes `owner_user_id = @owner`. `ListAsync` orders by `created_at DESC, id DESC`. `ExistsWithNameAsync` uses `lower(name) = lower(@name)`. `DeleteAsync` is a single `DELETE ... WHERE id = @id AND owner_user_id = @owner` that reports the affected row count. `LockOwnerAsync` runs `SELECT id FROM users WHERE id = @owner FOR UPDATE` on the unit of work's transaction.
- **Covers:** F2.R3, F2.R4, F2.R5, F2.R9, F2.R12, F2.AC4 (case rule), F2.AC5, F2.AC6, F2.AC7.
- **Tests:** integration (Testcontainers, `FakeTimeProvider` for `created_at`):
  - trips created X, Y, Z list as Z, Y, X (F2.AC7); two trips with the identical timestamp list in a stable order;
  - user B's list never contains A's trips, and `GetAsync` and `DeleteAsync` by B on A's id return nothing and remove nothing (F2.AC6, F2.R9);
  - `Paris 2026` matches `paris 2026` and `PARIS 2026`, matches only the same user's trips, and does not match `Paris 2025` (F2.AC4);
  - **race:** at 49 trips, 20 parallel creates through `TripService` leave exactly 50 trips (F2.R4);
  - deleting a trip frees a place: at 50, delete one, then a create succeeds (F2.AC5);
  - **cascade contract (F2.R12):** the test creates a throwaway child table `probe_children (trip_id uuid REFERENCES trips(id) ON DELETE CASCADE)` and a grandchild with a default `NO ACTION` reference to it, inserts rows, deletes the trip, and asserts everything is gone in one statement. The DDL lives in the test, not in a migration.
- **Done when:** the tests pass and a search of `TripDao.cs` finds no query without `owner_user_id` (or, for the lock, the owner id) and no string-concatenated SQL.

### F2.T4 — Api: `TripsController`
- **Files:** `TripExpenseTracker.Api/Controllers/TripsController.cs`, `Trips/CreateTripRequest.cs`, `TripResponse.cs`, `TripDetailResponse.cs`; mapping of service results to status codes; `IntegrationTests/Api/TripsEndpointTests.cs`.
- **Change:** the four endpoints in Contract changes. They rely on F1's default-deny policy, with no `[AllowAnonymous]`. The owner id comes only from `ICurrentUser`, never from the request. Results map to the codes in Contract changes. The detail response returns the three counts as `0`. A non-GUID id returns the same `404` as a missing trip.
- **Covers:** F2.R1–R9, F2.AC1–AC7, F2.AC10 (server side), F2.AC11, F2.AC15 (API half: `401` when logged out).
- **Tests:** integration (`WebApplicationFactory` + Testcontainers, `FakeTimeProvider`, users registered through F1's endpoints):
  - create `Paris 2026` returns `201` and the trip is first in `GET /api/trips` (F2.AC1);
  - spaces only returns `400` `name_required` and nothing is created; 101 characters returns `400` `name_too_long`; 100 characters returns `201` (F2.AC2, F2.AC3);
  - `paris 2026` after `Paris 2026` returns `409` `duplicate-trip-name` and creates nothing; with `confirmDuplicate: true` it returns `201` and both trips are listed (F2.AC4);
  - at 50 trips, a create returns `409` `trip-limit-reached`; after one delete the create returns `201` (F2.AC5);
  - **race through HTTP:** at 49 trips, two simultaneous creates produce one `201` and one `409` (F2.R4);
  - user B's list excludes A's trips. B's `GET` and `DELETE` on A's trip return a `404` whose status, `type` and body equal those for a random missing id (F2.AC6, F2.R9);
  - trips created X, Y, Z come back Z, Y, X (F2.AC7);
  - the detail response has the name and three zero counts (F2.AC8, API half);
  - `DELETE` returns `204`, then `GET` and a second `DELETE` return `404` (F2.AC10, F2.AC11);
  - every endpoint returns `401` with no session (F2.AC15, F1.R9); logging out and retrying returns `401`;
  - the API exposes no `PUT`, `PATCH` or `POST` on `/api/trips/{id}` (F2.R8): `405` or `404`.
- **Done when:** the tests pass and no endpoint under `/api/trips` accepts an owner id from the client.
- _Needs human review: authorization and ownership checks._

### F2.T5 — Replace the F1 placeholder with the trip list page (touches F1 code)
- **Files:** `app/src/web/src/app/trips/trip-list/trip-list.component.{ts,html,scss}`, `trips.service.ts`, `app.routes.ts`; **F1 files changed:** `trips-placeholder.component.*` (removed), F1.T13 front-end specs and F1.T15 e2e specs that assert the placeholder.
- **Change:**
  - `/trips` now loads the list page. Shows "Loading your trips…" while fetching. Shows the empty state "You have no trips yet. Create your first trip to start tracking shared expenses." when the list is empty (F2.R11). Otherwise a real `<ul>` of trips newest first, each trip name a link to `/trips/{id}` (F2.R5).
  - The page heading is "Your trips" (mockup `F2 · Your trips.png`). The log-out button stays in the shared header from F1.T13.
  - The guard from F1 protects the route. A logged-out visitor is redirected with "Please log in to see your trips." (F1.R15, F2.AC15).
  - After login and after registration the user lands here (F2.R10, F2.AC14).
  - The F1 specs that assert the placeholder text "You're logged in. Your trips will appear here." are replaced by specs that assert the trip list. This is a spec-driven change: F2.R10 states that the list replaces F1.R16. The F1 e2e steps that end on the placeholder end on the trip list instead.
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

### F2.T8 — End-to-end tests
- **Files:** `app/e2e/trips.spec.ts` and shared helpers (registering a unique user per test, as in F1.T15).
- **Change:** Playwright against the local stack and a non-shared database. Each test registers its own user.
- **Covers:** F2.AC1, F2.AC4, F2.AC5 (via the API for setup), F2.AC8, F2.AC9, F2.AC10, F2.AC12, F2.AC13, F2.AC14, F2.AC15, F2.AC6.
- **Tests:**
  - register, then see the trip list with the empty state and the create form (F2.AC12, F2.AC14);
  - create `Paris 2026`, then a same-name trip shows the warning, Cancel creates nothing, Create anyway adds a second one (F2.AC1, F2.AC4);
  - open a trip and see its name, the Delete button and no edit control (F2.AC8, F2.AC13);
  - delete with Cancel keeps the trip, delete with confirmation removes it, shows "Trip deleted." and the opened id now shows "This trip no longer exists." (F2.AC9, F2.AC10, F2.AC11);
  - a second user cannot see the first user's trips and sees "This trip no longer exists." for the first user's trip URL (F2.AC6);
  - the limit: create 50 trips through the API, then the form shows the limit message; delete one and create succeeds (F2.AC5);
  - logged out, `/trips` redirects to the log-in page (F2.AC15).
- **Done when:** the suite passes headless with no state shared between tests.

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F2.R1 | T2, T4, T6 | AC1 | T4 create then list; T6 form submit |
| F2.R2 | T1, T2, T4, T6 | AC2, AC3 | T2 trim and length unit tests; T4 `400` codes; T6 field errors |
| F2.R3 | T2, T3, T4, T6 | AC4 | T3 case-insensitive match; T4 `409` then confirmed `201`; T6 warning and buttons |
| F2.R4 | T2, T3, T4, T6 | AC5 | T2 count at 49/50/51; T3 and T4 parallel creates; T6 limit message |
| F2.R5 | T1, T3, T4, T5 | AC7 | T3 and T4 order Z, Y, X; T5 list order |
| F2.R6 | T4, T7 | AC8 | T4 detail response; T7 trip page and Delete button |
| F2.R7 | T4, T7 | AC9, AC10 | T7 dialog copy, counts, Cancel and confirm; T4 `204` |
| F2.R8 | T4, T7 | AC13 | T4 no `PUT`/`PATCH`; T7 no edit control; T8 |
| F2.R9 | T3, T4, T7 | AC6 | T3 DAO ownership; T4 identical `404` for foreign and missing; T7 not-found page |
| F2.R10 | T5 | AC14 | T5 and T8 login lands on the list |
| F2.R11 | T5, T6 | AC12 | T5 empty state; T6 empty state with form; T8 |
| F2.R12 | T1, T3 | AC10 | T3 cascade contract test with a probe table. F3–F6 plans add their own cascade tests. |

| Acceptance criterion | Tasks | Test |
|---|---|---|
| F2.AC1 | T4, T6, T8 | T4 create `Paris 2026` is first in the list; T8 |
| F2.AC2 | T2, T4, T6 | T2 spaces only; T4 `400`; T6 "Enter a trip name." |
| F2.AC3 | T2, T4, T6 | T2 and T4 at 101 and 100 characters; T6 |
| F2.AC4 | T3, T4, T6, T8 | T4 `409` then Create anyway; T6 Cancel and Create anyway; T8 |
| F2.AC5 | T2, T3, T4, T6, T8 | T4 limit at 50 and after a delete; T6 limit message; T8 |
| F2.AC6 | T3, T4, T7, T8 | T4 B gets the same `404` as a missing id; T7 message; T8 |
| F2.AC7 | T3, T4, T5 | T3 and T4 order Z, Y, X |
| F2.AC8 | T4, T7, T8 | T7 name and Delete button; T8 |
| F2.AC9 | T7, T8 | T7 Cancel sends no `DELETE`; T8 |
| F2.AC10 | T4, T7, T8 | T4 `204`; T7 "Trip deleted."; T8 |
| F2.AC11 | T4, T7, T8 | T4 `GET` after delete is `404`; T7 not-found copy; T8 |
| F2.AC12 | T5, T8 | T5 empty state with form; T8 |
| F2.AC13 | T4, T7, T8 | T7 no edit control; T8 |
| F2.AC14 | T5, T8 | T5 router lands on the list; T8 |
| F2.AC15 | T4, T5, T8 | T4 `401` on all endpoints; T5 redirect with message; T8 |

## Test plan
- **Unit (xUnit, Core):** name trimming, blank check and the 100-character limit (T2); the count check at 49, 50 and 51 (T2); duplicate and limit order in `TripService` (T2). The spec lists the capital-letter duplicate check and the list ordering as unit tests. They run in SQL, so they are tested at the DAO level (T3) against a real database, which keeps one implementation of each rule.
- **Integration (WebApplicationFactory + Testcontainers PostgreSQL):** migration (T1); DAO ownership, case rule, order, race and the cascade contract (T3); every endpoint, the identical `404`, the `401`s and the HTTP-level race (T4). Each test class gets its own database.
- **Front end (Angular Testing Library):** list, empty and loading states, login landing (T5); create form, duplicate warning, limit (T6); trip page, not-found and delete dialog with focus checks (T7).
- **E2E (Playwright):** the flows in T8.
- **Worked examples:** none. The product.md examples belong to F4–F6.
- **Manual checks:** phone-width layout of the list, form and dialog; keyboard-only through create, duplicate warning, open and delete; a screen reader reading the list item count, the "Delete trip {name}" buttons and the warning; 4.5:1 contrast on text and warnings; visible focus; focus trapped in the dialog and returned on close; the warning and limit message are readable without colour.

## Risks & mitigations
- **F1 is not built (blocker).** F2 starts only after F1.T1–T10 and T13 are done. If F1's names or behavior change while it is built, update F2's tasks. The F2 plan assumes F1's default-deny authorization, `ICurrentUser`, the unit of work and `PostgresFixture`.
- **Spec wording conflict about the card Delete action.** Scope and the mockup bullet in User flow say cards show "its Delete action", while the 2026-10-21 decision and F2.R6 and R7 put Delete only on the trip page. The plan follows the decision: cards show the trip name only, as a link, and no card menu. It suggests a one-line spec fix in `/spec-feature F2`, which would put the spec back to Draft. The plan does not need it to proceed.
- **Copy the spec does not give.** The not-found page needs a link label, and the plan uses "Your trips" (the list heading). The mockup's "Open trip →" button, the "3 trips · 1 still to settle" subtitle and the card "..." menu are not in the spec, so they are not built. The spec's literal "{payments} payment" is used with a singular label even for 0 or 2. Add copy to the spec if you want any of these.
- **F1 tests change in T5.** The placeholder assertions are replaced because F2.R10 says so. T5 is marked for human review for that reason. No F1 test is deleted or skipped.
- **Cross-feature work for F3–F6.** Their plans must (a) declare `trip_id ... ON DELETE CASCADE` and use `NO ACTION` for references between child tables (Contract changes), (b) add their own cascade test next to F2.T3's probe, and (c) make `peopleCount`, `expenseCount` and `paymentCount` in the trip detail real. F3–F6 are not planned yet, so this is recorded here.
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
