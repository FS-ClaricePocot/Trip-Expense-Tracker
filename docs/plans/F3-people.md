# F3 — People: Implementation plan

_Status: Approved · Last updated: 2026-10-21 · Spec: [F3-people](../specs/features/F3-people.md) (Approved, last updated 2026-10-21)_

## Overview
F3 lets a user add and remove the people on a trip. It adds the `people` table, three owner-checked endpoints, and a People panel on the trip page. Removal is refused for anyone who is in an expense or payment, both by the server and by the database. F4 and F6 create the tables that make that check meaningful, so F3 builds the check as an extension point and tests it with stand-ins. F3 is the third build step (F1 → F6).

## Prerequisites
- **Dependencies:**
  - **F1: planned, not built.** `app/` is empty. F3 reuses F1's `users` table, `ICurrentUser`, the default-deny authorization policy, the unit of work, `PostgresFixture`, `FakeTimeProvider` usage, `AuthService`, the HTTP interceptor and `problem-mapper.ts`.
  - **F2: planned, not built (blocking).** F3 cannot start until F2.T1–T4 and T7 exist. F3 reuses F2's `trips` table, `ITripDao.GetAsync(owner, id)` for the ownership check, the trip page at `/trips/:tripId`, the ON DELETE CASCADE convention (F2 plan, Contract changes) and the delete-dialog pattern.
  - F4, F5, F6: not started. F3 does not need them. F4 and F6 plug into F3's extension point (see Contract changes and Risks).
- **Other features' code:** F3.T5 changes F2's trip-detail count. F3.T6 changes F2's trip page and its front-end tests. Both have their own tasks below.
- **Setup:** none beyond F1's and F2's.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | `TripExpenseTracker.Data/Migrations/0003_create_people.sql` | `people` table, unique name per trip, ordering column |
| `TripExpenseTracker.Core` | `People/` (`Person`, `PersonNameRules`, `IPersonDao`, `IPersonUsageSource`, `PeopleService`, `PersonResults`) | Name rules, add/list/remove orchestration, in-use check |
| `TripExpenseTracker.Data` | `PersonDao.cs`; small change to `TripDao.cs` | Dapper queries with the owner in every `WHERE`; real `peopleCount` |
| `TripExpenseTracker.Api` | `Controllers/PeopleController.cs`, request and response records | Three endpoints under `/api/trips/{tripId}/people` |
| Angular (`app/src/web/src/app/people/`) | `people.service.ts`, `people-panel/`, `remove-person-dialog/`; edit `trips/trip-page/` | People panel on the trip page |
| Tests | `TripExpenseTracker.Tests`, `TripExpenseTracker.IntegrationTests`, web specs, `app/e2e` | Unit, integration, component and Playwright |

Rules from `CLAUDE.md` and `project-rules.md` that are easy to break here:
- **Ownership in the query.** Every `PersonDao` query joins `trips` and filters `owner_user_id = @owner`. A foreign trip and a missing trip look the same (F3.R10, F1.R9).
- **Parameterized SQL only.**
- **The DB enforces the rules too.** Name uniqueness is a unique index, and "in use" is a foreign key from the F4 and F6 tables (F3.R3, F3.R7). The app check alone is not enough because of races.
- **Order added matters for money.** F4 gives leftover cents in the order people were added (product.md R3), so the order must be exact and stable (see `seq` below).
- **Time from `TimeProvider`**, stored in UTC. Tests never read the real clock.
- **Logging.** `ILogger` only. Log ids, not names where an id will do. Nothing sensitive.
- **Copy** comes exactly from the spec's States & errors table.

## Contract changes
- **Database** (`0003_create_people.sql`):
  ```sql
  CREATE TABLE people (
    id       uuid PRIMARY KEY,
    trip_id  uuid NOT NULL REFERENCES trips(id) ON DELETE CASCADE,   -- F2.R12 convention
    name     text NOT NULL CHECK (char_length(name) BETWEEN 1 AND 100),
    added_at timestamptz NOT NULL,                                    -- set by the app from TimeProvider
    seq      bigint GENERATED ALWAYS AS IDENTITY,                     -- strict order added
    CONSTRAINT ux_people_id_trip UNIQUE (id, trip_id)                 -- lets F4/F6 use composite FKs for "same trip"
  );
  CREATE UNIQUE INDEX ux_people_trip_name ON people (trip_id, lower(name));   -- F3.R3
  CREATE INDEX ix_people_trip_seq ON people (trip_id, seq);
  ```
  - `added_at` is kept as the spec says. The list is ordered by `seq`, not `added_at`, because two adds can share a timestamp and the leftover-cent rule needs one exact order. A person removed and re-added gets a new row with a larger `seq` (F3.R5). **F4 must order people by `seq`** whenever it applies the leftover-cent rule.
  - `char_length` counts code points, the same unit F1 and F2 use.
- **In-use extension point (F3.R7).** Core defines `IPersonUsageSource` with `Kind` (`Expense` or `Payment`) and `GetPersonIdsInUseAsync(tripId)`. `PeopleService` asks every registered source and merges the results into `inExpense` and `inPayment` per person. F3 registers no sources, so nobody is in use until F4 and F6 each add one. Sources are only called after the trip is confirmed to belong to the user, and each filters by `trip_id`.
- **In-use check in the database.** F4 and F6 reference `people(id)` with the default `NO ACTION` (F2 plan, cascade convention), so deleting a person in use fails with PostgreSQL error `23503`. `PersonDao.DeleteAsync` maps that error to an "in use" result. This is the backstop for a race between a remove and an expense being added.
- **API** (JSON, all need a session through F1's default policy; the owner comes from `ICurrentUser`):
  | Method and path | Request | Success | Failure |
  |---|---|---|---|
  | `GET /api/trips/{tripId:guid}/people` | none | `200` `[{ id, name, addedAt, inExpense, inPayment }]`, in order added | `404` (trip not owned or missing, identical), `401` |
  | `POST /api/trips/{tripId:guid}/people` | `{ name }` | `201` `{ id, name, addedAt, inExpense: false, inPayment: false }` | `400` codes `name_required`, `name_too_long`. `409` ProblemDetails `type` `duplicate-person-name`. `404`. `401`. |
  | `DELETE /api/trips/{tripId:guid}/people/{personId:guid}` | none | `204` | `409` ProblemDetails `type` `person-in-use`, person stays. `404` for a foreign or missing trip, or a person not on that trip (same response). `401`. |
  - No `PUT` or `PATCH` exists (F3.R8). No limit on the number of people, so no paging (F3.R9).
  - The UI shows the "In expenses" tag when `inExpense` is true, otherwise "In payments" when `inPayment` is true (the mockup shows only "In expenses"; "In payments" is the spec's accepted wording).
  - The server never trusts the UI's tag. It checks the sources and the foreign key on every removal (F3.AC8).
- **Trip detail count:** `GET /api/trips/{id}` (F2) returns the real `peopleCount` from this task onward (F3.T5).
- **Trip list count:** `GET /api/trips` (F2) items gain `peopleCount` (int), computed in the same owner-filtered query (F3.T10). Existing fields are unchanged.
- **Browser storage:** none.

## Tasks

### F3.T1 — `people` migration
- **Files:** `app/src/api/TripExpenseTracker.Data/Migrations/0003_create_people.sql` (embedded resource).
- **Change:** the table and indexes from Contract changes. The table has no credential or contact columns, because a person has no account (F3.R4). No upper limit on rows (F3.R9).
- **Covers:** F3.R2 (stored length), F3.R3, F3.R4, F3.R5, F3.R9, F3.R11.
- **Tests:** integration (Testcontainers): the migration runs on top of `0001` and `0002`. A name of 0 or 101 characters is rejected. `Sam` then `sam` in the same trip is rejected, but the same name in another trip is accepted. A person for a missing trip is rejected. `seq` grows with each insert. The table's columns are exactly `id`, `trip_id`, `name`, `added_at`, `seq` (no account data). Running the migrations twice changes nothing.
- **Done when:** the tests pass on a fresh container.
- _Needs human review: database migration._

### F3.T2 — Core: name rules, `PeopleService` and the in-use extension point
- **Files:** `TripExpenseTracker.Core/People/Person.cs`, `PersonNameRules.cs`, `IPersonDao.cs`, `IPersonUsageSource.cs`, `PeopleService.cs`, `PersonResults.cs`.
- **Change:** pure logic and interfaces, no I/O.
  - `PersonNameRules.Normalize` trims the ends. Blank gives `name_required`. More than 100 code points gives `name_too_long`. Any characters allowed (F3.R2). It has the same behavior as F2's trip name rule but is its own small class.
  - `IPersonDao`: `ListAsync(owner, tripId)`, `AddAsync(owner, tripId, person)` (result: added, duplicate, or trip not found), `DeleteAsync(owner, tripId, personId)` (result: removed, in use, or not found).
  - `PeopleService`: every call first confirms the trip with F2's `ITripDao.GetAsync(owner, tripId)` for list; add and delete get the same guarantee from the owner join in the DAO. List merges the usage flags from all registered `IPersonUsageSource`s. Remove first asks the sources and refuses with `person-in-use` if any says yes, then calls the DAO, which can still refuse through the foreign key. `addedAt` comes from `TimeProvider`.
  - Removing the last person is allowed (spec, Edge cases, open question accepted for planning).
- **Covers:** F3.R1, F3.R2, F3.R5 (service), F3.R6, F3.R7, F3.R8 (no rename operation exists), F3.R9, F3.R10, F3.AC3, F3.AC4, F3.AC5, F3.AC8, F3.AC9.
- **Tests:** unit (xUnit, FluentAssertions, fake DAO and sources, `FakeTimeProvider`): `"  Jo  "` is saved as `Jo`; spaces only gives `name_required`; 100 characters pass and 101 fail; 100 emoji pass and 101 fail; with no sources registered, nobody is in use; an expense source flags a person `inExpense`; a payment source flags `inPayment`; both flags can be true together; removal is refused with `person-in-use` when any source reports the person, and the DAO is not called; removal goes through when no source reports them; the last person on a trip can be removed; a DAO "in use" result (the foreign-key backstop) becomes `person-in-use`; a trip that is not found gives "not found" for list, add and remove; `addedAt` equals the fake clock.
- **Done when:** the tests pass and `Core` has no reference to `Data`, Npgsql or ASP.NET.

### F3.T3 — Data: `PersonDao`
- **Files:** `TripExpenseTracker.Data/PersonDao.cs`, DI registration; `IntegrationTests/Data/PersonDaoTests.cs`.
- **Change:** implement `IPersonDao` with Dapper.
  - List: join `trips`, filter `t.id = @trip AND t.owner_user_id = @owner`, `ORDER BY p.seq`.
  - Add: `INSERT ... SELECT ... FROM trips t WHERE t.id = @trip AND t.owner_user_id = @owner`. Zero rows inserted means "trip not found". A `23505` on `ux_people_trip_name` means duplicate.
  - Delete: `DELETE FROM people p USING trips t WHERE p.id = @person AND p.trip_id = t.id AND t.id = @trip AND t.owner_user_id = @owner`. Zero rows means not found. A `23503` means in use.
- **Covers:** F3.R3, F3.R5, F3.R7, F3.R10, F3.R11, F3.AC1, F3.AC2, F3.AC10, F3.AC13 (DAO side), F3.AC14.
- **Tests:** integration (Testcontainers):
  - add `Sam` then `Alex`, the list is Sam, Alex (F3.AC1);
  - add `Sam`, then `sam` and ` SAM ` (trimmed by the service): duplicates, nothing added (F3.AC2);
  - **race:** 20 parallel adds of the same name leave exactly one person and the rest report duplicate;
  - remove `Alex`, add `Alex` again: Alex is last, with a larger `seq` (F3.AC10); two adds at the identical timestamp keep insertion order;
  - user B's add, list and delete against A's trip insert, return and remove nothing, with the same result as a missing trip; a person id from another trip is "not found" (F3.AC13);
  - **in use, database backstop (F3.R7):** a test-only table `probe_expense_people (person_id uuid REFERENCES people(id))` is created in the test (DDL in the test, not a migration), a row references Sam, and deleting Sam returns "in use" and Sam stays;
  - **cascade (F3.R11):** deleting the trip removes its people in the same statement, even when a probe child table with a `NO ACTION` reference to `people` also belongs to the trip (F3.AC14);
  - a trip of 150 people lists in order (no limit, F3.R9).
- **Done when:** the tests pass and a search of `PersonDao.cs` finds no query without the `trips` owner join and no string-concatenated SQL.

### F3.T4 — Api: `PeopleController`
- **Files:** `TripExpenseTracker.Api/Controllers/PeopleController.cs`, `People/AddPersonRequest.cs`, `PersonResponse.cs`; DI wiring (no usage sources registered); `IntegrationTests/Api/PeopleEndpointTests.cs`.
- **Change:** the three endpoints in Contract changes. They rely on F1's default-deny policy with no `[AllowAnonymous]`. The owner comes only from `ICurrentUser`. Service results map to the status codes in Contract changes. A non-GUID id is the same `404` as a missing one.
- **Covers:** F3.R1–R3, F3.R5–R10, F3.R11 (through the API), F3.AC1–AC10 (server side), F3.AC13, F3.AC14.
- **Tests:** integration (`WebApplicationFactory` + Testcontainers, `FakeTimeProvider`, users registered through F1, trips created through F2):
  - add `Sam` then `Alex`, `GET` lists Sam then Alex (F3.AC1);
  - `sam` after `Sam` returns `409` `duplicate-person-name` and the list is unchanged (F3.AC2);
  - 101 characters returns `400` `name_too_long`, 100 returns `201` (F3.AC3); `  Jo  ` is stored as `Jo` (F3.AC4); spaces only returns `400` `name_required` (F3.AC5);
  - two simultaneous adds of the same name give one `201` and one `409`;
  - remove an unused person returns `204` and the list no longer has them (F3.AC6);
  - **in use, with stand-ins until F4 and F6 exist (F3.AC8, F3.AC9):** the test factory registers a stub `IPersonUsageSource` for each kind. The list shows `inExpense: true` for the person the expense stub reports, `inPayment: true` and `inExpense: false` for the person only the payment stub reports, and `DELETE` on either returns `409` `person-in-use` with the person still listed. A separate test uses the probe table from F3.T3 to prove the `409` also happens when no source reports the person (the race backstop);
  - remove `Alex` then add `Alex`: Alex is last (F3.AC10);
  - the last person on a trip can be removed and the list is then empty;
  - user B gets a `404` with the same status, `type` and body as for a random missing trip, on list, add and delete, and nothing changes (F3.AC13, F3.R10); a `personId` from another trip returns the same `404`;
  - deleting the trip through F2's endpoint, then `GET` people returns `404`, and the people rows are gone (F3.AC14);
  - all three endpoints return `401` with no session; `PUT` and `PATCH` on a person return `405` or `404` (F3.R8).
- **Done when:** the tests pass and no endpoint accepts an owner id or a usage flag from the client.
- _Needs human review: authorization and ownership checks._

### F3.T5 — F2 code: real `peopleCount` in the trip detail (touches F2 code)
- **Files:** `TripExpenseTracker.Data/TripDao.cs` (detail query), `IntegrationTests/Api/TripsEndpointTests.cs` (new test only).
- **Change:** the detail query adds a count of the trip's `people` rows to `peopleCount`. `expenseCount` and `paymentCount` stay `0` until F4 and F6. No existing F2 assertion changes, because a trip with no people still reports `0`.
- **Covers:** F2.R7 (counts), F3.R11 (the delete dialog shows the people that will be deleted).
- **Tests:** integration: after adding 3 people, `GET /api/trips/{id}` returns `peopleCount: 3`; a trip with none returns `0`; another user's trip count is not reachable (`404`). The existing F2.T4 tests still pass unchanged.
- **Done when:** the new test passes and the F2 test run is green with no edits to existing tests.

### F3.T6 — People panel on the trip page, read-only (touches F2 code)
- **Files:** `app/src/web/src/app/people/people.service.ts`, `people/people-panel/people-panel.component.{ts,html,scss}`, spec; **F2 files changed:** `trips/trip-page/trip-page.component.{ts,html}` and its spec.
- **Change:**
  - `PeoplePanel` takes the trip id, loads `GET .../people` and shows: a heading "People" with the count (mockup), "Loading people…" while loading, a real `<ul>` of people in the order returned, and the helper text "Names are unique on a trip. People in an expense or payment can't be removed." (F3.R12).
  - Each row shows the name (the mockup's initial-letter avatar is decorative and hidden from screen readers). A person with `inExpense` shows the tag "In expenses". A person with only `inPayment` shows "In payments". Otherwise the row has a remove control (added in F3.T8). The tag is text.
  - An empty list shows "No one is on this trip yet. Add the people who will share its expenses." (F3.R12).
  - A `404` from the list tells the trip page that the trip is gone. The trip page then shows its own "This trip no longer exists." (F2 copy), so the text is not shown twice.
  - The trip page embeds the panel below the trip name. Its F2 front-end tests need an HTTP stub for the new people request. No assertion is removed or loosened.
  - A network or server error shows "Something went wrong. Please try again." in the panel.
- **Covers:** F3.R7 (tag), F3.R10 (front end), F3.R12, F3.AC8 and F3.AC9 (tags), F3.AC11 (no rename control), F3.AC12, F3.AC13 (message).
- **Tests:** front end (Angular Testing Library, mocked HTTP): the loading text shows first; three people render as a `list` with three `listitem`s in the order returned; a person with `inExpense` shows "In expenses" and no remove control; a person with only `inPayment` shows "In payments"; a person with both shows "In expenses"; the helper text and count show; an empty list shows the empty state; a `404` makes the trip page show "This trip no longer exists." once; a `500` shows the error text; there is no rename control. The existing F2.T7 trip page tests pass with the HTTP stub added.
- **Done when:** the tests pass and a reviewer confirms that the only changes to F2's trip page specs are the added stub.
- _Needs human review: edits existing F2 tests (project rules)._

### F3.T7 — Add a person
- **Files:** `people/people-panel/` (add form), `people.service.ts`, `problem-mapper.ts` additions, specs.
- **Change:** a Reactive Form in the panel with the visible label "Add a person", the example placeholder "Morgan" (not the label), and an **Add** button (mockup).
  - Client checks mirror F3.R2: "Enter a name." and "A name can be at most 100 characters." The server codes map to the same copy.
  - A `409` `duplicate-person-name` shows on the field: "{name} is already on this trip." using the trimmed name the user typed. Nothing is added.
  - While sending, the button is disabled and shows "Adding…". On success the person appears at the end of the list, the field clears and no message shows. The new person has no remove restriction.
  - A `404` makes the trip page show the trip-gone message. Other errors show "Something went wrong. Please try again." above the form.
  - Errors are linked to the field with `aria-describedby` and announced.
- **Covers:** F3.R1, F3.R2, F3.R3, F3.R9, F3.R12, F3.AC1–AC5; spec, Accessibility.
- **Tests:** front end: both field errors show with exact copy and no request is sent; 100 characters send, 101 do not; `  Jo  ` sends `Jo`; a `409` shows "sam is already on this trip." for the typed `sam`; the button shows "Adding…" and is disabled during the request; a new person appears last and the field clears; a `404` hands over to the trip page; a `500` shows the error; the form works with the keyboard alone.
- **Done when:** the tests pass and all text matches the spec's States & errors table word for word.

### F3.T8 — Remove a person
- **Files:** `people/remove-person-dialog/remove-person-dialog.component.{ts,html,scss}`, `people-panel/` (remove control and messages), specs.
- **Change:**
  - Each removable row has a remove control (the "×" in the mockup) with the accessible name "Remove {name}", at least 24×24 CSS pixels.
  - It opens an Angular CDK dialog, built like F2's delete dialog and reusing its layout (mockup `F2 · Delete trip confirmation.png`): `role="dialog"`, title "Remove {name}?", body "They will no longer be on this trip. You can add them again later.", buttons **Remove** and **Cancel**. The dialog traps focus, **Escape** closes it and focus returns to the control that opened it. Initial focus goes to **Cancel**.
  - **Cancel** closes the dialog and keeps the person. **Remove** calls `DELETE`. On `204` the row disappears and "{name} was removed from the trip." shows in a `role="status"` region in the panel. After a successful removal focus moves to the panel heading, because the control that had it no longer exists.
  - On `409` the dialog closes, the list reloads (the person now shows their tag) and the message "{name} is in an expense or payment, so they can't be removed." shows in the panel.
  - On a `404` the panel reloads the list. If the list request returns `404`, the trip is gone and the trip page shows its message. Otherwise the person was already removed (for example in another tab) and the reloaded list shows that. The spec gives no copy for this case, so no message is added.
  - Other errors show "Something went wrong. Please try again." inside the dialog.
  - Removing the last person leaves the empty state.
- **Covers:** F3.R6, F3.R7, F3.R8, F3.R12, F3.AC6, F3.AC7, F3.AC8, F3.AC11; spec, Accessibility.
- **Tests:** front end: the remove control's accessible name is "Remove Sam"; the dialog shows the exact title, body and buttons; Cancel closes it and no `DELETE` is sent (F3.AC7); Escape closes it and focus returns to the control; focus stays inside while open; Remove sends `DELETE`, the row disappears and "Sam was removed from the trip." shows (F3.AC6); a `409` shows the in-use message, reloads the list and the row now has its tag (F3.AC8); a `404` on delete with a successful reload just refreshes the list; a `404` on the reload hands over to the trip page; a `500` shows the error in the dialog; removing the last person shows the empty state; there is no rename control (F3.AC11).
- **Done when:** the tests pass and the dialog copy matches the spec exactly.

### F3.T9 — End-to-end tests
- **Files:** `app/e2e/people.spec.ts`.
- **Change:** Playwright against the local stack and a non-shared database, as in F1.T15 and F2.T8. Each test registers its own user and creates its own trip.
- **Covers:** F3.AC1, F3.AC2, F3.AC6, F3.AC7, F3.AC10, F3.AC11, F3.AC12, F3.AC13, F3.AC14.
- **Tests:**
  - a new trip shows the empty state and the add field (F3.AC12);
  - add `Sam` and `Alex`, the list is in that order (F3.AC1); `sam` shows "sam is already on this trip." (F3.AC2);
  - remove with Cancel keeps the person, remove with confirmation removes them (F3.AC7, F3.AC6); remove `Alex` and add `Alex` puts them last (F3.AC10);
  - there is no rename control (F3.AC11);
  - a second user opening the first user's trip URL sees "This trip no longer exists." and no people (F3.AC13);
  - delete the trip and its people are gone: opening the old URL shows the not-found message (F3.AC14).
  - The "In expenses" and "In payments" rows are not covered here, because expenses and payments do not exist yet. F4 and F6 add those end-to-end checks.
- **Done when:** the suite passes headless with no state shared between tests.

### F3.T10 — Trip list: people count on each card (touches F2 code)
- **Files:** `TripExpenseTracker.Data/TripDao.cs` (list query, F2.T3), `TripExpenseTracker.Api/Controllers/TripsController.cs` (list item, F2.T4), `app/src/web/src/app/trips/trip-list/` (card text, F2.T5), `trip-count-text.ts` (wording), specs; `IntegrationTests/Api/TripsEndpointTests.cs` (new assertions only).
- **Change:**
  - The owner-filtered trip list query adds `peopleCount` as a correlated count of each trip's `people` rows, in the same query, so the list does not make one query per card.
  - `GET /api/trips` items gain `peopleCount`. Existing fields are unchanged.
  - Card text (F3.R13): "{n} people", "1 person" for one, and "No people yet" for none. The wording is a pure function in `trip-count-text.ts`.
  - The count comes from the same `people` rows the People panel lists, so the two match (F3.R14).
  - Build after F2.T5 and F2.T6 exist. It can be built any time after F3.T3.
- **Covers:** F3.R13, F3.R14, F3.AC15, F3.AC16, and F2.R5 (list order and ownership stay the same).
- **Tests:**
  - unit: the wording: 0 gives "No people yet", 1 gives "1 person", 3 gives "3 people" (F3.AC15, F3.AC16);
  - integration: a trip with three people lists `peopleCount` 3, and its People panel lists 3 (F3.AC15, F3.R14); a trip with none lists 0 (F3.AC16); another user's trips do not appear; the list order is unchanged; the list makes one query;
  - front end: the card shows "3 people", "1 person" and "No people yet" for the three cases.
- **Done when:** the tests pass, and the F2 list tests pass with only the new assertions added.
- _Needs human review: adds a field to an F2 response and changes an F2 card (project rules: approved F2 code)._

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F3.R1 | T2, T3, T4, T7 | AC1 | T3 and T4 add then list; T7 form |
| F3.R2 | T1, T2, T4, T7 | AC3, AC4, AC5 | T2 trim and length unit tests; T4 `400` codes; T7 field errors |
| F3.R3 | T1, T3, T4, T7 | AC2 | T1 unique index; T3 parallel adds; T4 `409`; T7 message |
| F3.R4 | T1 | none | T1 table has no account columns |
| F3.R5 | T1, T3, T4 | AC1, AC10 | T3 `seq` order and re-add last; T4 re-add last |
| F3.R6 | T2, T4, T8 | AC6, AC7 | T4 `204`; T8 dialog, Cancel and Remove |
| F3.R7 | T2, T3, T4, T6, T8 | AC8, AC9 | T2 sources and refusal; T3 foreign-key backstop; T4 stand-ins; T6 tags; T8 `409` message |
| F3.R8 | T2, T4, T8 | AC11 | T4 no `PUT` or `PATCH`; T6 and T8 no rename control; T9 |
| F3.R9 | T1, T3, T4, T7 | none | T3 150 people; T1 no row limit |
| F3.R10 | T3, T4, T6 | AC13 | T3 owner join; T4 identical `404`; T6 trip-gone message |
| F3.R11 | T1, T3, T4, T5 | AC14 | T3 cascade statement; T4 trip delete removes people; T5 count |
| F3.R12 | T6, T7 | AC12 | T6 panel, helper text, empty state; T7 add field |
| F3.R13 | T10 | AC15, AC16 | T10 wording unit tests; card front-end tests; list `peopleCount` |
| F3.R14 | T10 | AC15 | T10 list count matches the panel |

| Acceptance criterion | Tasks | Test |
|---|---|---|
| F3.AC1 | T3, T4, T7, T9 | T3 and T4 Sam then Alex; T7; T9 |
| F3.AC2 | T3, T4, T7, T9 | T3 and T4 `sam` after `Sam`; T7 copy; T9 |
| F3.AC3 | T2, T4, T7 | T2 and T4 at 101 and 100; T7 |
| F3.AC4 | T2, T4, T7 | T2 and T4 `  Jo  ` becomes `Jo`; T7 |
| F3.AC5 | T2, T4, T7 | T2 and T4 spaces only; T7 "Enter a name." |
| F3.AC6 | T4, T8, T9 | T4 `204` and list; T8 row disappears; T9 |
| F3.AC7 | T8, T9 | T8 Cancel sends no `DELETE`; T9 |
| F3.AC8 | T2, T3, T4, T6, T8 | T2 refusal; T3 backstop; T4 `409` with stand-in and probe table; T6 tag; T8 message |
| F3.AC9 | T2, T4, T6 | T2 payment-only flag; T4 `inPayment` and `409`; T6 "In payments" |
| F3.AC10 | T3, T4, T9 | T3 and T4 re-added Alex is last; T9 |
| F3.AC11 | T4, T6, T8, T9 | T6 and T8 no rename control; T4 no `PUT`; T9 |
| F3.AC12 | T6, T9 | T6 empty state and add field; T9 |
| F3.AC13 | T3, T4, T6, T9 | T4 identical `404`; T6 message; T9 |
| F3.AC14 | T3, T4, T9 | T3 cascade; T4 people gone after trip delete; T9 |
| F3.AC15 | T10 | T10 "3 people" on the card and integration count |
| F3.AC16 | T10 | T10 "No people yet" on the card and integration count of 0 |

## Test plan
- **Unit (xUnit, Core):** name trimming, blank check and the 100-character limit (T2); usage flags from sources and the "in use" refusal (T2); the last person is removable (T2). The spec lists the capital-insensitive duplicate check and the added-time ordering as unit tests. They live in SQL (a unique index and `seq`), so they are tested against a real database in T3, which keeps one implementation of each rule.
- **Integration (WebApplicationFactory + Testcontainers PostgreSQL):** migration (T1); DAO ownership, duplicate race, order, foreign-key backstop and cascade (T3); every endpoint, the identical `404`, the `401`s and the stand-in "in use" cases (T4); the trip-detail count (T5). Each test class gets its own database.
- **Front end (Angular Testing Library):** panel states and tags (T6); add form (T7); remove dialog, messages and focus (T8).
- **E2E (Playwright):** the flows in T9.
- **Trip list card:** the people count wording and its match with the People panel (T10).
- **Worked examples:** none. The leftover-cent rule (product.md R3, example 2) belongs to F4, which depends on the order F3 guarantees.
- **Manual checks:** phone-width layout of the panel and dialog; keyboard-only through add, remove and cancel; a screen reader reading the list count, "Remove {name}" buttons and tags; 4.5:1 contrast on text and tags; visible focus; focus trapped in the dialog and returned on close.

## Risks & mitigations
- **F1 and F2 are not built (blocker).** F3 starts only after F2.T1–T4 and T7 exist. The plan assumes the names in F1's and F2's plans. If they change, update F3's tasks.
- **Last-person rule is not in product.md.** Removing the last person is allowed, as you decided on 2026-10-21. product.md does not state it. If it changes, T2, T4 and T8 each lose one test and gain a rule.
- **F4 and F6 must plug in (their plans are not written).** Each must (a) implement `IPersonUsageSource` for its table and register it, (b) reference `people(id)` with the default `NO ACTION`, not `RESTRICT`, so the trip cascade still works, (c) use `UNIQUE (id, trip_id)` as a composite key if they want the database to enforce "same trip", (d) replace F3's stand-in "in use" tests with real ones and add the "In expenses" and "In payments" end-to-end checks, (e) make `expenseCount` and `paymentCount` real in the trip detail, and (f) order people by `seq` for the leftover-cent rule.
- **`seq` goes beyond the spec's wording.** The spec says `added_at` orders people. The plan keeps `added_at` and orders by an identity column because two adds can share a timestamp, and F4's money split depends on one exact order.
- **F2 list and card are touched in T10.** The trip list gains `peopleCount` and a card line. T10 is marked for human review, and it adds assertions without changing the F2 ones.
- **F2 front-end tests are touched in T6.** The change only adds an HTTP stub for the people request. T6 is marked for human review for that reason. No F2 test is deleted, skipped or weakened.
- **Copy the spec does not give.** There is no copy for a person already removed in another tab, so the list simply refreshes. After a successful removal, focus moves to the panel heading, which the spec does not state. The people count in the panel header is shown as a number, as in the mockup. Add copy to the spec if you want any of these changed.
- **"In payments" wording** is accepted only "for now" in the spec and may change when the design is revised. It lives in one front-end place.
- **Duplicate message uses the typed name.** `{name}` is the trimmed text the user typed, so `sam` shows "sam is already on this trip." The spec allows either.
- **Open questions in the spec:** none. The last-person question was closed on 2026-10-21.

## Out of scope
- Renaming a person, person accounts, logins and invitations (spec, Out).
- Expenses, payers, participants and the split (F4), balances (F5), suggested payments and payments (F6).
- The real "in use" data and the "In expenses" and "In payments" end-to-end checks, which F4 and F6 add.
- A limit on the number of people (spec: none).
- Expense count and total on the trip card (F4.R17–R18, F4.T11).

## Decisions log
- 2026-10-21: Does the open question about removing the last person block planning? → No. Plan it as allowed, as the spec proposes. The question stays open in the spec.
- 2026-10-21: _(Proposed in the plan, not asked; no product behavior changes.)_ Paths `GET/POST /api/trips/{tripId}/people` and `DELETE /api/trips/{tripId}/people/{personId}`. A duplicate is a `409` `duplicate-person-name` and an in-use removal is a `409` `person-in-use`. Validation returns the codes `name_required` and `name_too_long`, and the front end owns the copy (as in F1 and F2). The list returns `inExpense` and `inPayment` flags and the UI chooses the tag. `id` is a `uuid`.
- 2026-10-21: _(Proposed.)_ Order added is a database identity column `seq`, kept alongside `added_at`, so the leftover-cent order is exact.
- 2026-10-21: _(Proposed.)_ "In use" is an `IPersonUsageSource` extension point in Core plus a `NO ACTION` foreign key from F4 and F6, with the foreign-key error mapped to the refusal. F3 tests it with stand-ins.
- 2026-10-21: _(Proposed.)_ The remove dialog is its own component that reuses F2's dialog layout and pattern, rather than extracting a shared component, so no F2 code changes for it. Initial focus is on **Cancel**.
- 2026-10-21: _(Proposed.)_ The trip card's people count is a correlated count in the F2 list query (F3.T10), so the list stays one query, and the wording lives in one pure function.
