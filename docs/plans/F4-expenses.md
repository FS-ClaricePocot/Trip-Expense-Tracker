# F4 — Expenses: Implementation plan

_Status: Approved · Last updated: 2026-10-22 · Spec: [F4-expenses](../specs/features/F4-expenses.md) (Approved, last updated 2026-10-21)_

## Overview
F4 lets a user record an expense on a trip: a description, an amount, a payer and the people who share it. Each expense is split equally to the cent, with leftover cents going to the earliest-added participants. It adds the `expenses` and `expense_participants` tables, three owner-checked endpoints, and the Expenses panel with its add form, list, delete dialog and shares dialog. It also registers the real "in use" source for expenses with F3's people service. Tests are unit tests only (project rules); the SQL is checked in code review. F4 is the fourth build step (F1 → F6). F5 and F6 read expenses, so the money rules in this plan are the ones the whole app depends on.

## Prerequisites
- **Dependencies:**
  - **F1: planned, not built (blocking).** Reused: `users`, `ICurrentUser`, the default-deny policy, `IUnitOfWork`, `IDbConnectionFactory`, `FakeTimeProvider`, the ProblemDetails middleware, the HTTP interceptor, `problem-mapper.ts`.
  - **F2: planned, not built (blocking).** Reused: `trips`, `ITripDao`, the trip page at `/trips/:tripId`, the ON DELETE CASCADE convention, the delete-dialog pattern (F2.T7).
  - **F3: planned, not built (blocking).** F4 needs F3.T1 (`people` with `seq` and `UNIQUE (id, trip_id)`), F3.T2 (`IPersonUsageSource`), F3.T3 (`PersonDao`), and F3.T6 (the people list service and panel that the add form reads participants from).
  - F5, F6: not started. F5 and F6 depend on F4.
- **Other features' code:** F4.T6 changes code F2 and F3 own: the trip-detail counts (F2.T4, F3.T5) and the people service's registered usage sources (F3.T2). F4.T6 is marked for human review.
- **Setup:** none beyond F1's and F2's.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | `TripExpenseTracker.Data/Migrations/0004_create_expenses.sql` | `expenses`, `expense_participants`, composite foreign keys to `people` |
| `TripExpenseTracker.Core` | `Money/MoneyParser.cs`, `Expenses/ExpenseShares.cs`, `Expenses/ExpenseRules.cs`, `Expenses/Expense.cs`, `Expenses/IExpenseDao.cs`, `Expenses/ExpenseService.cs`, `Expenses/ExpenseResults.cs` | The one money parser, the split, rules, and orchestration. No I/O. |
| `TripExpenseTracker.Data` | `ExpenseDao.cs`, `ExpenseUsageSource.cs` (implements `IPersonUsageSource`); small change to `TripDao.cs` | Dapper queries with owner checks; the real "in use" source; real trip counts |
| `TripExpenseTracker.Api` | `Controllers/ExpensesController.cs`, request and response records; DI registration of `ExpenseUsageSource`; `Money` JSON conventions | Three endpoints under `/api/trips/{tripId}/expenses` |
| Angular (`app/src/web/src/app/expenses/`) | `expenses.service.ts`, `expenses-panel/`, `add-expense-form/`, `delete-expense-dialog/`, `shares-dialog/`, `money.ts` (display only) | Expenses panel on the trip page, beside People |
| Tests | `TripExpenseTracker.Tests`, web specs | Unit tests only (project rules): Core and controllers with fakes, Angular components with mocked HTTP. Database behaviour is checked in code review. |

Rules from `CLAUDE.md` and `project-rules.md` that are easy to break here:
- **Money is `long` cents end to end.** The only parser that turns text into cents is `MoneyParser` in Core (CLAUDE.md, Architecture rules). The API takes the amount as text, and the response returns `amountCents` as an integer. The front end formats cents for display only and never does arithmetic on floats.
- **Ownership in every query.** Every expense query joins `trips` and filters on the owner (F4.R15). Foreign and missing trips return the same `404`.
- **One transaction.** Adding an expense writes the expense and its participants in one unit of work (CLAUDE.md, Architecture rules).
- **Parameterized SQL only.**
- **Time from `TimeProvider`**, stored as UTC. Tests never read the real clock.
- **Logging.** `ILogger` only. Never log amounts together with person names at information level.
- **Copy** comes exactly from the spec's States & errors table.

## Contract changes
- **Database** (`0004_create_expenses.sql`):
  ```sql
  CREATE TABLE expenses (
    id               uuid PRIMARY KEY,
    trip_id          uuid NOT NULL REFERENCES trips(id) ON DELETE CASCADE,
    description      text NOT NULL CHECK (char_length(description) BETWEEN 1 AND 100),
    amount_cents     bigint NOT NULL CHECK (amount_cents > 0),
    payer_person_id  uuid NOT NULL,
    created_at       timestamptz NOT NULL,              -- from TimeProvider
    seq              bigint GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT ux_expenses_id_trip UNIQUE (id, trip_id),
    CONSTRAINT fk_expenses_payer FOREIGN KEY (payer_person_id, trip_id)
      REFERENCES people (id, trip_id)                 -- NO ACTION: blocks removal (F3.R7)
  );
  CREATE INDEX ix_expenses_trip_seq ON expenses (trip_id, seq);
  CREATE INDEX ix_expenses_payer ON expenses (payer_person_id);

  CREATE TABLE expense_participants (
    expense_id uuid NOT NULL,
    trip_id    uuid NOT NULL,
    person_id  uuid NOT NULL,
    PRIMARY KEY (expense_id, person_id),
    CONSTRAINT fk_participant_expense FOREIGN KEY (expense_id, trip_id)
      REFERENCES expenses (id, trip_id) ON DELETE CASCADE,
    CONSTRAINT fk_participant_person FOREIGN KEY (person_id, trip_id)
      REFERENCES people (id, trip_id)                 -- NO ACTION: blocks removal (F3.R7)
  );
  CREATE INDEX ix_expense_participants_person ON expense_participants (person_id);
  ```
  - `trip_id` on `expense_participants` exists only so the composite foreign keys can prove "same trip" (F4.R4, R5, product.md R2). The database, not only the app, refuses a participant or payer from another trip.
  - Shares are **not stored**. They are computed from `amount_cents`, the participants, and their `people.seq` order. This is safe because expenses cannot be edited (F4.R12) and a person in an expense cannot be removed (F3.R7), so the participants and their order never change after saving.
  - The "at least one participant" rule is enforced by the service in the same transaction (user decision, 2026-10-21). The database does not enforce it.
- **Money parsing (Core, `MoneyParser`).** Accepts text made of an optional single `.` with 1–2 digits after it, digits before it, after trimming outer spaces. Result: whole cents, or one of `amount_required`, `amount_invalid` (not a decimal), `amount_too_many_decimals`, `amount_not_positive`, `amount_too_large` (does not fit `long`). Everything else, including `.5`, `10.`, `+5`, `1,000` and `1e3`, is `amount_invalid` (see Risks).
- **Share rule (Core, `ExpenseShares`).** With `n` participants listed in `people.seq` order, each gets `amount / n` (integer division), and the first `amount % n` participants get one extra cent. The shares sum to exactly the amount (product.md R3, F4.R7).
- **API** (JSON, all need a session through F1's default policy; the owner comes from `ICurrentUser`):
  | Method and path | Request | Success | Failure |
  |---|---|---|---|
  | `GET /api/trips/{tripId:guid}/expenses` | none | `200` `{ total, count, expenses: [{ id, description, amountCents, payer: {id, name}, participantCount, participants: [{id, name, shareCents}], createdAt }] }`, newest first (`seq DESC`). `total` is the sum of `amountCents` (F4.R13). | `404` (identical for foreign, missing, or non-GUID trip). `401`. |
  | `POST /api/trips/{tripId:guid}/expenses` | `{ description, amount, payerPersonId, participantPersonIds: [guid] }`. `amount` is decimal text. | `201` the same item shape as in the list | `400` ProblemDetails codes: `description_required`, `description_too_long`, `amount_required`, `amount_invalid`, `amount_too_many_decimals`, `amount_not_positive`, `amount_too_large`, `payer_required`, `payer_not_on_trip`, `participants_required`, `participant_not_on_trip`, `participant_duplicate`. `409` `no_people_on_trip` (F4.AC10). `409` `people_changed` when a person is removed between the check and the save (see Risks). `404`. `401`. |
  | `DELETE /api/trips/{tripId:guid}/expenses/{expenseId:guid}` | none | `204` | `404` (identical for foreign, missing, or not-on-this-trip). `401`. |
  - No `PUT` or `PATCH` exists (F4.R12).
  - No limit on expenses per trip, so no paging (F4.R16). The list is returned whole.
  - The API never returns an icon field. The icon rule is an interim generic icon on every row (F4.R11).
- **In-use source (F3.R7).** `ExpenseUsageSource` implements `IPersonUsageSource` with `Kind = Expense`. It returns the payers and participants of the trip's expenses. A payer counts as "in an expense", because the spec says the payer's reference blocks removal (F4 Data & contract).
- **Trip detail:** `GET /api/trips/{id}` (F2) now returns the real `expenseCount` and the total. `paymentCount` stays `0` until F6.
- **Trip list:** `GET /api/trips` (F2) items gain `expenseCount` (int) and `totalCents` (0 when there are no expenses), computed in the same owner-filtered query (F4.T11). Existing fields are unchanged. The card shows the total only when `expenseCount` is greater than 0.
- **Browser storage:** none.

## Tasks

### F4.T1 — `expenses` and `expense_participants` migration
- **Files:** `app/src/api/TripExpenseTracker.Data/Migrations/0004_create_expenses.sql` (embedded resource).
- **Change:** the tables, indexes and composite foreign keys in Contract changes. The payer and participant keys use the `NO ACTION` default, not `RESTRICT`, so a trip delete can remove people and expenses in one statement (F2 cascade convention).
- **Covers:** F4.R3 (stored cents and sign), F4.R2 (stored description length), F4.R4 and F4.R5 (same-trip keys), F4.R8 (ordering column), F4.R14 (cascade), F3.R7 (references block removal).
- **Tests:** none automated (unit tests only). **Review:**
  - `amount_cents > 0` and the 1–100 character description checks;
  - the payer and participant keys are composite `(person_id, trip_id)` references to `people (id, trip_id)`, so a person from another trip is refused, and they use the default `NO ACTION`, not `RESTRICT` (F3.R7 backstop, trip cascade still works);
  - `expenses.trip_id` cascades from `trips`, and participants cascade from `expenses` (F2.R12, F4.R9, F4.R14).
- **Done when:** the API starts in Development and DbUp applies `0004` to the local database without errors, and a reviewer has read the SQL. **Manual check:** deleting a trip with expenses succeeds and removes them.
- _Needs human review: database migration._

### F4.T2 — Core: money parser, share rule and expense rules
- **Files:** `TripExpenseTracker.Core/Money/MoneyParser.cs`, `Expenses/ExpenseShares.cs`, `Expenses/ExpenseRules.cs`, unit tests.
- **Change:** pure functions.
  - `MoneyParser.ParseCents(string)` follows Contract changes. It is the only place that turns text into cents.
  - `ExpenseShares.Compute(long amountCents, IReadOnlyList<(Guid PersonId, long Seq)>)` returns one share per participant, in `seq` order, with leftover cents to the earliest. It refuses an empty list (F4.R5).
  - `ExpenseRules` trims the description (F4.R2), refuses blank (`description_required`) and over 100 code points (`description_too_long`), requires a payer (`payer_required`), requires at least one participant (`participants_required`), and refuses duplicate participants (`participant_duplicate`). Whether a payer or participant is on the trip is checked in the service (T3), not here.
- **Covers:** F4.R2, F4.R3, F4.R5, F4.R6 (a payer who is not a participant gets no share), F4.R7.
- **Tests:** unit (xUnit, FluentAssertions):
  - **Worked example 2:** 10.00 among Sam, Alex, Jo in `seq` order gives 3.34, 3.33, 3.33 (F4.AC2);
  - 90.00 among three gives 30.00 each (F4.AC1);
  - 0.01 among three gives 0.01, 0.00, 0.00 (Edge cases);
  - shares always sum to the amount, checked over many random amounts and counts with a fixed seed;
  - `10` is `1000` cents, `10.5` is `1050`, `10.50` is `1050`, `0.01` is `1`, `10.505` is refused, `0` and `-1` are refused, `12.50` is refused in the form `1,250`;
  - `.5`, `10.`, `+5`, `1e3`, `abc` and spaces inside a number are refused as `amount_invalid` (see Risks);
  - the largest `long` cents value is accepted, and one more is `amount_too_large`;
  - description trimming: `"  Dinner  "` is `Dinner`; only spaces is refused; 100 code points pass and 101 fail; 100 emoji pass and 101 fail (F4.AC5);
  - no participants is refused (F4.AC7), and a missing payer is refused (F4.AC6).
- **Done when:** the tests pass and `Core` has no reference to `Data`, Npgsql or ASP.NET. No `double` or `float` appears in these files.

### F4.T3 — Core: `ExpenseService` and `IExpenseDao`
- **Files:** `TripExpenseTracker.Core/Expenses/Expense.cs`, `IExpenseDao.cs`, `ExpenseService.cs`, `ExpenseResults.cs`; unit tests.
- **Change:**
  - `Add`: validate with T2 → open unit of work → load the trip's people for the owner (refuse with `no_people_on_trip` if none) → check payer and participants are on the trip (`payer_not_on_trip`, `participant_not_on_trip`) → insert the expense and its participants with `addedAt` from `TimeProvider` → commit.
  - `List`: returns the expenses and the total, and computes each participant's share with T2.
  - `Delete`: removes one expense on the owner's trip; reports not-found for anything else (F4.R15).
  - The total is summed in Core with checked arithmetic. If an add would push the trip's total past `long.MaxValue`, it is refused with `amount_too_large` (see Risks).
- **Covers:** F4.R1, F4.R4, F4.R5 (service side), F4.R8, F4.R9 (service side), F4.R12, F4.R13, F4.R15, F4.AC8, F4.AC9 (server default needs none), F4.AC10, F4.AC11, F4.AC14 (service side).
- **Tests:** unit with fake DAO and `FakeTimeProvider`:
  - Example 1 shares: Dinner 90.00 with Sam, Alex, Jo gives 30.00 each (F4.AC1);
  - a payer not among the participants is accepted and gets no share (F4.AC8, F4.R6);
  - no people on the trip is refused and nothing is inserted (F4.AC10);
  - a payer or participant from another trip is refused;
  - `addedAt` equals the fake clock; the list is newest first by `seq` (F4.AC11);
  - the total equals the sum of amounts, and an add that would overflow the total is refused;
  - delete of an unknown id returns not-found.
- **Done when:** the tests pass.

### F4.T4 — Data: `ExpenseDao` and `ExpenseUsageSource`
- **Files:** `TripExpenseTracker.Data/ExpenseDao.cs`, `ExpenseUsageSource.cs`, DI registration.
- **Change:**
  - Insert expense and participants in the unit of work's transaction. A `23503` from the composite keys maps to `people_changed` (a person was removed after the check).
  - List: join `trips`, filter on the owner, order by `seq DESC`, and load participants with their `people.seq` for T2's share rule.
  - Delete: `DELETE ... WHERE id = @id AND trip_id = @trip AND owner = @owner`; zero rows is not-found.
  - Total: `SUM(amount_cents)` for the trip, checked in Core (T3).
  - `ExpenseUsageSource.GetPersonIdsAsync(tripId)`: the payers and participants of the trip's expenses, filtered by `trip_id` (F3.R7).
- **Covers:** F4.R8, F4.R9, F4.R13, F4.R14, F4.R15, F4.AC12, F4.AC13, F4.AC14 (DAO side), F4.AC16.
- **Tests:** none automated (unit tests only; the DAO is SQL). **Code review checklist:**
  - list, add and delete join `trips` and filter on the owner, so a user can't list, add to or delete from another user's trip (F4.AC14, F4.R15);
  - the expense and its participants are inserted on the unit of work's transaction, so a failure leaves nothing half-written;
  - a `23503` from the composite keys maps to `people_changed` (a person removed between the check and the save);
  - the list orders by `seq DESC` and loads each participant's `people.seq` for the share rule (F4.AC11);
  - `ExpenseUsageSource` returns payers **and** participants, filtered by `trip_id`, so a payer who isn't a participant still counts as in use;
  - no paging (F4.R16); parameters only, no string-concatenated SQL.
- **Done when:** the build passes, the checklist is ticked by a reviewer, and a manual run behaves as expected: add, list, delete; then delete the only expense involving Riley and Riley can be removed (F4.AC13).
- _Needs human review: ownership in SQL (no automated test covers it)._

### F4.T5 — Api: `ExpensesController`
- **Files:** `TripExpenseTracker.Api/Controllers/ExpensesController.cs`, `Expenses/CreateExpenseRequest.cs`, `ExpenseResponse.cs`, `ExpenseListResponse.cs`; DI wiring; `TripExpenseTracker.Tests/Api/ExpensesControllerTests.cs`.
- **Change:** the three endpoints in Contract changes. They rely on F1's default-deny policy with no `[AllowAnonymous]`. The owner comes only from `ICurrentUser`. Results map to the codes in Contract changes. A non-GUID id is the same `404`. `amount` is passed to `MoneyParser` only, and the response returns integer cents.
- **Covers:** F4.R1–R5, F4.R8, F4.R9, F4.R12, F4.R13, F4.R15, F4.R16, F4.AC1–AC4 (server), F4.AC5 (server), F4.AC6, F4.AC7, F4.AC8, F4.AC10, F4.AC11, F4.AC12, F4.AC14, F4.AC16, F4.AC17 (API shape: description, amount, payer, participantCount, no per-person field on the row).
- **Tests:** unit (xUnit, the controller built directly with a fake `ExpenseService` result source and a fake `ICurrentUser`, no web host or database):
  - an added expense returns `201` with integer `amountCents` and the participants' `shareCents` from the service (F4.AC1, F4.AC2 response shape);
  - every validation result maps to `400` with its code (`amount_not_positive`, `amount_too_many_decimals`, `description_required`, `description_too_long`, `payer_required`, `participants_required` and the rest) (F4.AC3–AC7);
  - `no_people_on_trip` and `people_changed` map to `409` (F4.AC10);
  - delete returns `204`; "not found" maps to the same `404` ProblemDetails as an id that isn't a GUID (F4.AC12, F4.AC14);
  - the list response carries `total`, `count` and each row's `participantCount`, with no per-person field on the row (F4.AC16, F4.AC17);
  - the amount text is handed to the service unchanged (parsing stays in `MoneyParser`); the owner id is always the fake `ICurrentUser`'s.
- **Review:** no `[AllowAnonymous]`, so F1's default-deny policy returns `401` when logged out; no `PUT` or `PATCH` actions exist (F4.AC15, F4.R12).
- **Done when:** the tests pass and no endpoint accepts an owner id or a computed share from the client.
- _Needs human review: authorization and ownership checks._

### F4.T6 — F2 and F3 code: real counts and the expense "in use" source (touches F2 and F3 code)
- **Files:** `TripExpenseTracker.Data/TripDao.cs` (detail query); `TripExpenseTracker.Api/Program.cs` (register `ExpenseUsageSource`).
- **Change:**
  - The trip detail returns the real `expenseCount` and total in place of `0`, inside the same owner-filtered query. `paymentCount` stays `0` until F6.
  - Registers `ExpenseUsageSource` with the people service, so a person in an expense shows "In expenses" and can't be removed (F3.AC8).
- **Covers:** F3.R7 (real data for the expense case, F3.AC8), F2.R7 (counts), F4.R13 (total in the detail), F4.R14.
- **Tests:** none new automated: F3.T2's unit tests already cover how the people service uses any registered source. **Review:** the counts sit inside the owner-filtered detail query, and the source is registered. **Manual check:** add an expense with Sam, then Sam's People row shows "In expenses" and removing Sam is refused; the delete dialog shows "1 expenses" (spec copy).
- **Done when:** the review and manual check pass, and the existing F2 and F3 tests still pass unchanged.
- _Needs human review: changes F2 and F3 code (project rules)._

### F4.T7 — Expenses panel: list, total and row text
- **Files:** `app/src/web/src/app/expenses/expenses.service.ts`, `expenses-panel/expenses-panel.component.{ts,html,scss}`, `money.ts` (display formatting only), specs; `trips/trip-page/trip-page.component.{ts,html}` (adds the panel).
- **Change:**
  - The panel shows the heading "Expenses" with the count (mockup `F2–F5 · Trip page expenses, people, balances.png`), the total, and "Loading expenses…" while loading.
  - The list is a real `<ul>`. Each row shows the description, the amount formatted from cents, the payer, and "{payer} paid · split {N} ways" (F4.R10). The row has no per-person share.
  - Every row shows the generic money icon, `aria-hidden`, because the keyword rule is not decided (F4.R11 interim).
  - The empty state shows "No expenses yet. Add the first one below." (States table).
  - A `404` from the list makes the trip page show "This trip no longer exists." once.
  - Errors show "Something went wrong. Please try again." in the panel.
  - The row has no edit control (F4.R12).
- **Covers:** F4.R8, F4.R10, F4.R11 (interim), F4.R12, F4.R13, F4.R15 (front-end side), F4.AC11, F4.AC15, F4.AC16, F4.AC17.
- **Tests:** front end (Angular Testing Library, mocked HTTP):
  - the loading text shows first;
  - three expenses render as a `list` in the order returned, with the text "Alex paid · split 3 ways";
  - the total and count equal the sum of the amounts (F4.AC16);
  - the row shows no per-person share and no edit control (F4.AC15, F4.AC17);
  - the generic icon is `aria-hidden` and has no keyword logic;
  - `money.ts` formats `1050` as `10.50` and `0` as `0.00`, with no float arithmetic;
  - empty state; `404` handed to the trip page; `500` shows the error.
- **Done when:** the tests pass and the Expenses panel sits on the trip page beside the People panel.

### F4.T8 — Add-expense form
- **Files:** `expenses/add-expense-form/add-expense-form.component.{ts,html,scss}`, spec; `problem-mapper.ts` additions.
- **Change:** a Reactive Form inside the Expenses panel with the visible labels "What was it for?", "Amount", "Paid by" and "Split between", and the **Add expense** button. The helper text "Split equally · leftover cents go to people in the order they were added" shows under the checkboxes (mockup, final).
  - The "Paid by" select is native and starts with no choice selected (F4.R4, F4.AC9).
  - One checkbox per trip person, each labelled with the person's name, all checked when the form opens (F4.R5, F4.AC9). The group has a visible legend.
  - Client checks mirror the server codes and use the copy from the States table: "Enter a description.", "A description can be at most 100 characters.", "Enter an amount.", "Enter an amount like 12.50.", "Enter an amount greater than 0.00.", "An amount can have at most 2 decimal places.", "That amount is too large to record.", "Choose who paid.", "Choose at least one person to split between." (above the checkboxes).
  - The amount input is `type="text"` with `inputmode="decimal"`. It sends the text unchanged to the server, which is the authority (F4.R3).
  - With no people on the trip, the form is disabled with "Add people to the trip before adding an expense." (F4.AC10).
  - While sending, the button is disabled and shows "Adding…". On success the expense appears at the top, the form resets with every person checked again and no payer selected, and no message shows.
  - Errors are linked to their fields with `aria-describedby`.
- **Covers:** F4.R1–R6 (form side), F4.AC3–AC7, F4.AC9, F4.AC10; spec, Accessibility.
- **Tests:** front end: each error appears with its exact copy, and no request is sent for it; `10.505` shows the decimal-places error; `0` shows the greater-than error; `  ` shows the description error; no payer shows "Choose who paid."; all unchecked shows the participant error above the checkboxes; the form opens with everyone checked and no payer; the form is disabled with no people; the button shows "Adding…" during the request; a success resets the form; the form works with the keyboard alone.
- **Done when:** the tests pass and every visible message matches the States table word for word.

### F4.T9 — Delete dialog and shares dialog
- **Files:** `expenses/delete-expense-dialog/delete-expense-dialog.component.{ts,html,scss}`, `expenses/shares-dialog/shares-dialog.component.{ts,html,scss}`, specs; the expense row (`expenses-panel`) gains the trash control and the row selection.
- **Change:**
  - **Delete:** the trash control's accessible name is "Delete {description}". It opens an Angular CDK dialog, built like F2's delete dialog and reusing its layout (mockup `F3, F4, F6 · Dialogs and messages.png`, top-left): title "Delete {description}?", body "This removes the expense and changes the balances. You can't undo this.", buttons **Delete expense** and **Cancel**. Initial focus goes to **Cancel**. Focus is trapped, **Escape** closes the dialog and focus returns to the trash control.
  - **Cancel** keeps the expense. **Delete expense** calls `DELETE`. On `204` the row disappears, the panel shows "{description} was deleted." in a `role="status"` region, and the total and count update. On `404` the panel shows "This expense no longer exists.". Other errors show "Something went wrong. Please try again." inside the dialog.
  - **Shares:** selecting an expense row (not the trash control) opens a dialog titled "Shares for {description}" that lists each participant's name and `shareCents` in `seq` order, with a "Total" line, and a **Close** button (mockup `F3, F4, F6 · Dialogs and messages.png`, top-centre). Close returns focus to the row. The trip page is unchanged.
- **Covers:** F4.R9, F4.R10a, F4.AC12, F4.AC18; spec, Accessibility.
- **Tests:** front end: the trash control's accessible name includes the description; the delete dialog shows the exact title, body and buttons; Cancel sends no `DELETE` (F4.AC12); **Delete expense** removes the row, updates the total, and shows the deleted message; a `404` shows "This expense no longer exists."; Escape closes the dialog and focus returns to the trash control; the shares dialog lists participants in `seq` order with shares and total (F4.AC18); Close leaves the trip page unchanged; focus stays inside both dialogs while open.
- **Done when:** the tests pass and both dialogs' copy matches the spec and the mockup.

### F4.T11 — Trip list: expense count and total on each card (touches F2 code)
- **Files:** `TripExpenseTracker.Data/TripDao.cs` (list query, F2.T3), `TripExpenseTracker.Api/Controllers/TripsController.cs` (list item, F2.T4), `app/src/web/src/app/trips/trip-list/` (card lines, F2.T5), `expense-count-text.ts` (wording), specs; `TripExpenseTracker.Tests/Api/TripsControllerTests.cs` (new assertions only).
- **Change:**
  - The owner-filtered trip list query adds `expenseCount` and `totalCents` as correlated aggregates per trip, in the same query. `totalCents` is `COALESCE(SUM(amount_cents), 0)`, read through a checked cast to `long`. F4.T3 refuses any add that would push a trip's total past the stored range, so the cast cannot fail for valid data.
  - `GET /api/trips` items gain `expenseCount` and `totalCents`. Existing fields are unchanged.
  - Card line (F4.R17): "{n} expenses", "1 expense" for one, and "no expenses yet" for none. The wording is a pure function in `expense-count-text.ts`.
  - "Total spent" and the amount, formatted with `money.ts` from cents, are shown only when `expenseCount` is greater than 0 (F4.R18).
  - Build after F2.T5 and F2.T6 exist, and after F4.T7 (the expense panel) so the card's expense line and the trip page agree.
- **Covers:** F4.R17, F4.R18, F4.AC19, F4.AC20, F4.AC21, and F2.R5 (list order and ownership stay the same).
- **Tests:**
  - unit: the wording: 0 gives "no expenses yet", 1 gives "1 expense", 3 gives "3 expenses"; the total line is hidden at 0 (F4.AC21);
  - unit (Api): the list item carries `expenseCount` and `totalCents` from the service result;
  - front end: the card shows "3 expenses" with "Total spent" and the amount formatted from cents (1050 becomes 10.50); "1 expense" for one (F4.AC20); zero shows "no expenses yet" and no total line (F4.AC21).
- **Review:** the aggregates are correlated sub-queries inside the existing owner-filtered list query (one query, order unchanged). **Manual check:** a trip with three expenses shows "3 expenses" and the same total as its Expenses panel (F4.AC19).
- **Done when:** the tests pass, and the F2 list tests pass with only the new assertions added.
- _Needs human review: adds fields to an F2 response and changes an F2 card (project rules: approved F2 code)._

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F4.R1 | T3, T5, T8 | AC1 | T3 Example 1; T5 `201`; T8 form |
| F4.R2 | T1, T2, T5, T8 | AC5 | T2 trim and length; T5 `400` codes; T8 errors |
| F4.R3 | T1, T2, T5, T8 | AC3, AC4 | T1 review (`> 0` check); T2 parser table; T5; T8 |
| F4.R4 | T1, T3, T5, T8 | AC6, AC9 | T1 review (same-trip payer key); T3 payer on trip; T5; T8 no default |
| F4.R5 | T1, T2, T3, T5, T8 | AC7, AC9 | T1 review (same-trip participant key); T2 empty list; T5; T8 all checked |
| F4.R6 | T3 | AC8 | T3 payer not a participant |
| F4.R7 | T2, T3 | AC1, AC2 | T2 Example 2 and sums; T3 shares in the list |
| F4.R8 | T1, T3, T4, T7 | AC11 | T3 newest first; T4 review (`seq DESC`); T7 list |
| F4.R9 | T3, T4, T5, T9 | AC12 | T5 `204`; T9 dialog |
| F4.R10 | T7 | AC17 | T7 row text, no per-person share |
| F4.R10a | T9 | AC18 | T9 shares dialog |
| F4.R11 | T7 | none | T7 generic icon, aria-hidden; keyword rule not built (open) |
| F4.R12 | T5, T7 | AC15 | T5 review (no `PUT`/`PATCH`); T7 no edit control |
| F4.R13 | T3, T5, T6, T7 | AC16 | T3 total; T5 total in the response; T6 review; T7 panel total |
| F4.R14 | T1, T4, T6 | none | T1 review (cascade); T4 manual check (Riley freed); T6 review |
| F4.R15 | T3, T4, T5, T7 | AC14 | T4 review (owner join); T5 identical `404`; T7 `404` handover |
| F4.R16 | T4 | none | T4 review (no paging) |
| F4.R17 | T11 | AC19, AC20, AC21 | T11 wording unit tests; card front-end tests; `expenseCount` mapping |
| F4.R18 | T11 | AC19, AC21 | T11 `totalCents` mapping; card hides the total at 0 |

| Acceptance criterion | Tasks | Test |
|---|---|---|
| F4.AC1 | T2, T3, T5 | T2 90.00 by three; T3 Example 1; T5 response |
| F4.AC2 | T2, T5 | T2 Example 2; T5 response |
| F4.AC3 | T2, T5, T8 | T2 `0` refused and `0.01` accepted; T5; T8 |
| F4.AC4 | T2, T5, T8 | T2 `10.505` refused and `10.5` is `1050`; T5; T8 |
| F4.AC5 | T2, T5, T8 | T2 trim and length; T5; T8 |
| F4.AC6 | T2, T5, T8 | T2 payer required; T5; T8 |
| F4.AC7 | T2, T5, T8 | T2 empty participants; T5; T8 |
| F4.AC8 | T3 | T3 payer not a participant |
| F4.AC9 | T8 | T8 defaults |
| F4.AC10 | T3, T5, T8 | T3 no people refused; T5 `409`; T8 disabled form |
| F4.AC11 | T3, T4, T7 | T3 order; T4 review; T7 |
| F4.AC12 | T5, T9 | T5 `204` and `404`; T9 Cancel |
| F4.AC13 | T4, T6 | T4 and T6 manual check (Riley freed after the expense is deleted) |
| F4.AC14 | T4, T5 | T4 review (owner join); T5 identical `404`; manual check with two users |
| F4.AC15 | T5, T7 | T5 review (no `PUT`); T7 no edit control |
| F4.AC16 | T3, T5, T7 | T3 total; T5 total; T7 panel |
| F4.AC17 | T5, T7 | T5 response shape; T7 row text |
| F4.AC18 | T9 | T9 shares dialog |
| F4.AC19 | T11 | T11 card shows count and total; manual match with the panel |
| F4.AC20 | T11 | T11 "1 expense" |
| F4.AC21 | T11 | T11 "no expenses yet" and no total line |

## Test plan
Unit tests only (project rules).
- **Unit (xUnit, Core):** money parser (T2); share rule, including Example 2 and the sum-to-amount property (T2); expense rules (T2); expense service with fake DAO, Example 1 shares, payer-not-participant, the total and its overflow check (T3); card wording (T11).
- **Unit (xUnit, Api):** controller result-to-status mapping, every `400` code, the identical `404`, the response shape with integer cents, and the owner always taken from `ICurrentUser` (T5, T11).
- **Front end (Angular Testing Library, mocked HTTP):** panel, row text and total (T7); add form and its messages (T8); dialogs and focus (T9); card lines (T11).
- **Code review (not testable with unit tests):** T1 migration (checks, composite keys, `NO ACTION`, cascades); T4 checklist (owner join, one transaction, `23503` mapping, `seq` order, usage source includes payers); T6 and T11 counts and totals inside the owner-filtered queries.
- **Existing suites:** F1's integration and Playwright tests must still pass. Run them once at the end of F4.
- **Worked examples:**
  - **Example 1** (shares part): Dinner 90.00 by Sam with Sam, Alex and Jo gives 30.00 each. Named tests: T2 and T3 (F4.AC1). Example 1's balances and suggested payments are for F5 and F6.
  - **Example 2** (rounding): 10.00 among Sam, Alex and Jo gives 3.34, 3.33 and 3.33. Named test: T2 (F4.AC2).
  - **Example 3** belongs to F6.
- **Manual checks:** phone-width layout of the panel, form and dialogs; keyboard-only through add, delete and both dialogs; a screen reader reading the "split N ways" text, the checkbox names and the error messages; 4.5:1 contrast on text and errors; visible focus; focus trapped in dialogs and returned on close.

## Risks & mitigations
- **No automated test covers the SQL.** Ownership, the same-trip keys, the one-transaction add, the `23503` mapping and the cascade are verified only by review and a manual run. T4 is marked for human review. The money rules themselves (parsing, shares, totals) stay fully unit-tested in Core.
- **F1, F2 and F3 are not built (blocker).** F4 starts only after F3's tasks T1–T3 and T6 exist, and the F1 and F2 tasks they depend on. If their names change, update F4's tasks.
- **Icon keyword lists are open (you chose to plan around it).** The plan builds the generic icon only. Keyword matching is not built and needs its own task once the lists are decided. The spec's F4.R11 interim rule is unchanged.
- **Money-text refusals beyond the spec.** The spec says "a decimal with at most 2 decimal places" and gives examples. The plan refuses `.5`, `10.`, `+5`, `1,000`, `1e3` and spaces inside a number. If you want thousands separators or a leading decimal point accepted, that is a product decision: tell me, and it goes into the spec.
- **Singular copy.** The row text "split N ways" has no wording for one participant. The plan uses "split 1 ways" until the spec gives it. Tell me which wording you want.
- **Trip total overflow.** The spec refuses an amount too large to store. It does not say what happens when many valid amounts push the trip's total past the stored range. The plan refuses the add with `amount_too_large`. Confirm or change this.
- **Person removed during an add.** If a person is removed between the check and the save, the database refuses the insert. The plan returns `409` `people_changed`, and the UI shows "Something went wrong. Please try again." because the spec has no copy for this. Confirm the copy.
- **Payer counts as "in an expense".** F3.R7 says a person in any expense cannot be removed, and F4 says the payer's reference blocks removal. The plan follows both: a payer who is not a participant cannot be removed either.
- **Shares are computed, not stored.** This is safe only because expenses cannot be edited (F4.R12) and their participants cannot be removed (F3.R7). If editing or uneven splits are added later (product.md, After the core), this needs a new decision.
- **Two identical expenses are accepted** (spec Edge cases). No uniqueness is added.
- **Card line order.** The trip card shows the people count (F3.T10) and the expense count and total (F4.T11) in the mockup's order: people, then expenses, then the total. Each line is built on its own, so the card component sets the order. Check the layout against the mockup when it is built.
- **Open questions in the spec:** the icon keyword list, planned around as above.

## Out of scope
- Editing an expense, uneven splits, custom shares, categories, dates, and currency (spec, Out, and product.md, After the core).
- The icon keyword matching rule (F4.R11 open question).
- Balances and the "Left to settle" total (F5), and suggested payments, payments and the payment "in use" source (F6).
- Real `paymentCount` in the trip detail (F6).
- Choosing an icon from a list (product.md Goals would need a change first).
- The "Add the first expense" button on a trip card with no expenses (spec, Out, decided 2026-10-21).

## Decisions log
- 2026-10-21: Does the open icon keyword question block planning? → No. Plan the generic icon only, and leave keyword matching as a later task.
- 2026-10-21: Where should "at least one participant" be enforced? → In the app and its transaction only, not in a database trigger.
- 2026-10-21: _(Proposed in the plan, not asked; no product behavior changes.)_ Shares are computed from the amount, the participants and `people.seq`, not stored. Safe because expenses cannot be edited and participants cannot be removed.
- 2026-10-21: _(Proposed.)_ Amounts are text in the API, parsed only by `MoneyParser` in Core. Responses return integer cents. Money is `long` throughout, and the front end formats cents for display only.
- 2026-10-21: _(Proposed.)_ Composite foreign keys to `people (id, trip_id)` enforce the same-trip rule in the database, and `NO ACTION` keeps the trip cascade working. Payers and participants block person removal through these keys.
- 2026-10-21: _(Proposed.)_ Error codes are ProblemDetails codes, as in F1–F3. The front end owns the copy from the States table.
- 2026-10-21: _(Proposed.)_ The payer is an "in expense" reference for F3.R7, even when not a participant.
- 2026-10-21: _(Proposed.)_ The interim generic icon is a constant in the front end. The API returns no icon field.
- 2026-10-21: _(Proposed.)_ The trip card's expense count and total are correlated aggregates in the F2 list query (F4.T11), so the list stays one query. The total is read through a checked cast, and the add refusal (F4.T3) keeps it in range.
- 2026-10-22: Test scope reduced to unit tests only, following the project rules change. T1, T4 and T6 lose their integration tests and get review checklists and manual checks instead; T5's endpoint tests become controller unit tests with fakes; T11's integration assertions become a unit mapping test and a manual check; F4.T10 (Playwright end-to-end) is removed and its ID is not reused. The worked examples stay as Core unit tests (T2, T3). F1's existing integration and e2e tests stay and must pass. Status set back to Draft for re-approval.
- 2026-10-22: Approve the plan with unit tests only? → Yes. Status set to Approved.
