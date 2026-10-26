# F6 — Settle up: Implementation plan

_Status: Approved · Last updated: 2026-10-22 · Spec: [F6-settle-up](../specs/features/F6-settle-up.md) (Approved, last updated 2026-10-21)_

## Overview
F6 proposes the fewest payments the app can, using the balances from F5, and lets the user mark each suggestion as paid. A recorded payment changes the balances, so the suggestions update until every balance is zero. It adds the `payments` table, the settle-up and payment endpoints, and the Settle up view: the view switch, three summary tiles, the Suggested payments panel with its dialogs, and the Payments made list. It replaces the zero payment terms in F5's formula and registers the real "in use" source for payments with F3's people service. F6 is the last MVP build step (F1 → F6).

## Prerequisites
- **Dependencies:**
  - **F1: planned, not built (blocking).** Reused: `ICurrentUser`, the default-deny policy, `IUnitOfWork`, `FakeTimeProvider`, `problem-mapper.ts`.
  - **F2: planned, not built (blocking).** Reused: `trips`, the trip page, the delete-dialog pattern, the cascade convention.
  - **F3: planned, not built (blocking).** Reused: `people` and `seq`, `IPersonUsageSource` (F3.T2), and the People panel's "In payments" tag (F3.T6).
  - **F4: planned, not built (blocking).** Reused: `expenses`, `ExpenseDao`, `ExpenseShares`, the total (F4.R13), and the Expenses panel. F6 changes F4's add and delete code (see F6.T5).
  - **F5: planned, not built (blocking).** Reused: `BalanceCalculator`, `BalanceService`, the Balances panel (F5.T5), the summary tiles (F5.T6), and the trip-page layout (F5.T7). F6 changes the F5 formula to read payments.
- **Other features' code:** F6.T5 changes F2 (trip-detail `paymentCount`), F3 (payment usage source registered), F4 (trip lock in add and delete), and F5 (payments term in the formula and the tile's "in N payments"). Each is listed in T5 and marked for human review.
- **Setup:** none beyond the earlier features'.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | `TripExpenseTracker.Data/Migrations/0005_create_payments.sql` | `payments` table with composite keys to `people` |
| `TripExpenseTracker.Core` | `SettleUp/SuggestionRules.cs`, `SettleUp/SuggestedPayment.cs`, `SettleUp/Progress.cs`, `Payments/Payment.cs`, `Payments/PaymentRules.cs`, `Payments/IPaymentDao.cs`, `Payments/PaymentService.cs`, `Payments/SettleUpService.cs`, `IUnitOfWork.cs` (trip lock) | Suggestion rule, percentage, payment rules and service, trip lock |
| `TripExpenseTracker.Data` | `PaymentDao.cs`, `PaymentUsageSource.cs`, `TripLock.cs`; small changes to `BalanceDao.cs`, `ExpenseDao.cs`, `TripDao.cs` | Dapper queries, the real "in use" source, the trip lock, the payments read |
| `TripExpenseTracker.Api` | `Controllers/PaymentsController.cs`, `Controllers/SettleUpController.cs`, request and response records | Settle-up read, mark as paid, delete payment |
| Angular (`app/src/web/src/app/settle-up/`) | `settle-up.service.ts`, `trip-view-switch/`, `summary-tiles-settle/`, `suggested-payments-panel/`, `mark-payment-dialog/`, `delete-payment-dialog/`, `payments-made-list/`, `settle-up-view/`, `pluralize.ts`, `date-format.ts` | The Settle up view and its parts |
| Trip page | `app/src/web/src/app/trips/trip-page/` | Adds the view switch; shows either the Expenses view or the Settle up view |
| Tests | `TripExpenseTracker.Tests`, web specs | Unit tests only (project rules): Core and controllers with fakes, Angular components with mocked HTTP. Database behaviour, the trip lock and races are checked in code review. |

Rules from `CLAUDE.md` and `project-rules.md` that are easy to break here:
- **Money is `long` cents.** The suggestion rule, the percentage and the amounts are integer arithmetic. The percentage uses `Int128` for the multiplication, so it cannot overflow. The client sends the amount it saw as integer cents.
- **Multi-record writes in one transaction.** Marking a payment as paid does three things in one unit of work: lock the trip, recompute the suggestions, and insert the payment (F6.R18, CLAUDE.md).
- **The trip lock.** `IUnitOfWork` gains `LockTripAsync(tripId)`, which runs `SELECT id FROM trips WHERE id = @trip AND owner_user_id = @owner FOR UPDATE`. Mark, delete and the expense writes all take it. It serialises changes to a trip's money, so a suggestion is never checked against balances that are changing underneath it.
- **Ownership in every query.** Every payment query joins `trips` and filters on the owner (F6.R15). Foreign and missing trips return the same `404`.
- **Parameterized SQL only.**
- **Time from `TimeProvider`**, stored as UTC. The date is shown in the browser's local time zone (spec, Edge cases).
- **Logging.** `ILogger` only. Never log amounts together with names at information level.

## Contract changes
- **Database** (`0005_create_payments.sql`):
  ```sql
  CREATE TABLE payments (
    id               uuid PRIMARY KEY,
    trip_id          uuid NOT NULL REFERENCES trips(id) ON DELETE CASCADE,   -- F6.R17
    payer_person_id  uuid NOT NULL,
    payee_person_id  uuid NOT NULL,
    amount_cents     bigint NOT NULL CHECK (amount_cents > 0),              -- F6.R6
    marked_paid_at   timestamptz NOT NULL,                                  -- from TimeProvider
    seq              bigint GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT payer_not_payee CHECK (payer_person_id <> payee_person_id),  -- F6.R6
    CONSTRAINT fk_payments_payer FOREIGN KEY (payer_person_id, trip_id)
      REFERENCES people (id, trip_id),                                      -- NO ACTION: blocks removal (F3.R7)
    CONSTRAINT fk_payments_payee FOREIGN KEY (payee_person_id, trip_id)
      REFERENCES people (id, trip_id)                                       -- NO ACTION
  );
  CREATE INDEX ix_payments_trip_seq ON payments (trip_id, seq);
  CREATE INDEX ix_payments_payer ON payments (payer_person_id);
  CREATE INDEX ix_payments_payee ON payments (payee_person_id);
  ```
  - `payer_not_payee` is a database check, so the rule holds for any writer.
  - No `UNIQUE` on payments. Two identical payments are both kept (spec, Edge cases).
  - The payer and payee keys are composite, as in F4, so a payment from another trip is refused by the database.
- **Suggestion rule (Core, `SuggestionRules`):** the rule of product.md R6 as written, repeated until every balance is zero (F6.R2, F6.R4):
  1. Take the person with the largest debt (the most negative balance). On a tie, take the earliest added (lowest `seq`) (F6.R3).
  2. Take the person with the largest credit (the largest positive balance), with the same tie rule.
  3. Suggest `amount = min(debt, credit)` from the debtor to the creditor.
  4. Reduce both balances by `amount` and repeat.
  Each step makes at least one balance zero, so the loop ends. The output is deterministic. It is not the fewest possible payments in every case, and the spec accepts that (F6.R4).
- **Percentage (Core, `Progress`):** `paidBackCents` and `owedCents`, where `owedCents = paidBackCents + leftToSettleCents` (F6.R12).
  - If `owedCents` is `0`, the percentage is `null`, and the UI shows "Nothing to settle yet." (F6.AC14).
  - Otherwise it is `floor(paidBackCents × 100 / owedCents)` computed in `Int128`. Because `owedCents = paid + left`, it is exactly `100` only when `leftToSettleCents` is `0`. With `left > 0` it is at most `99` (F6.AC10).
- **Payment terms in F5 (Core, `BalanceCalculator`):** `paymentsMade` for a person is the sum of `amount_cents` where they are the payer, and `paymentsReceived` is the sum where they are the payee. `balance = paid − shares + paymentsMade − paymentsReceived` (F5.R1, F6.R10). This is the change that replaces F5's zero terms, so every F5 test still passes.
- **API** (JSON, all need a session through F1's default policy; the owner comes from `ICurrentUser`):
  | Method and path | Request | Success | Failure |
  |---|---|---|---|
  | `GET /api/trips/{tripId:guid}/settle-up` | none | `200` `{ suggestions: [{ payerPersonId, payerName, payeePersonId, payeeName, amountCents }], payments: [{ id, payerPersonId, payerName, payeePersonId, payeeName, amountCents, markedPaidAt }] (newest first), paidBackCents, owedCents, leftToSettleCents, suggestionCount, percentage (int or null), totalSpentCents, expenseCount }` | `404` (identical for foreign, missing or non-GUID trip). `401`. |
  | `POST /api/trips/{tripId:guid}/payments` | `{ payerPersonId, payeePersonId, amountCents }`: the suggestion the user saw | `201` `{ id, payerPersonId, payeePersonId, amountCents, markedPaidAt }` | `400` ProblemDetails codes `payer_payee_same`, `amount_not_positive`, `payer_not_on_trip`, `payee_not_on_trip`. `409` `suggestions_changed` when no current suggestion has this payer, payee and amount (F6.R18). `404`. `401`. |
  | `DELETE /api/trips/{tripId:guid}/payments/{paymentId:guid}` | none | `204` | `404` (identical for foreign trip and missing payment, F6.R15). `401`. |
  - No `PUT` or `PATCH` exists (F6.R9).
  - No paging, because the number of payments is not limited.
  - The Settle up view reads the Balances panel from `GET /api/trips/{tripId}/balances` (F5), unchanged (F6.R13).
- **In-use source (F3.R7):** `PaymentUsageSource` implements `IPersonUsageSource` with `Kind = Payment`. It returns the payers and payees of the trip's payments, filtered by `trip_id`.
- **Trip detail:** `GET /api/trips/{id}` (F2) returns the real `paymentCount`.
- **Browser storage:** none.

## Tasks

### F6.T1 — `payments` migration
- **Files:** `app/src/api/TripExpenseTracker.Data/Migrations/0005_create_payments.sql` (embedded resource).
- **Change:** the table and indexes in Contract changes. The payer and payee keys use `NO ACTION`, so the trip cascade still works (F2 convention).
- **Covers:** F6.R6 (stored cents, payer and payee rules, same trip), F6.R9 (no update path in the schema is needed), F6.R17 (cascade), F3.R7 (references block removal).
- **Tests:** none automated (unit tests only). **Review:**
  - `amount_cents > 0` and the `payer_not_payee` check;
  - the payer and payee keys are composite references to `people (id, trip_id)` with the default `NO ACTION`, so a person from another trip is refused and a person in a payment can't be removed (F6.AC12, F3.R7);
  - `trip_id` cascades from `trips` (F6.AC16, F6.R17); there is no `UNIQUE`, so identical payments are allowed.
- **Done when:** the API starts in Development and DbUp applies `0005` to the local database without errors, and a reviewer has read the SQL. **Manual check:** deleting a trip with payments succeeds.
- _Needs human review: database migration._

### F6.T2 — Core: suggestion rule, percentage and Left to settle
- **Files:** `TripExpenseTracker.Core/SettleUp/SuggestionRules.cs`, `SuggestedPayment.cs`, `Progress.cs`; unit tests.
- **Change:** pure functions over `(personId, seq, balanceCents)` lists.
  - `SuggestionRules.Compute` follows Contract changes exactly, including the tie rule on `seq`.
  - `Progress.Compute` returns `paidBackCents`, `owedCents`, and the percentage, or `null` when nothing is owed.
  - `suggestionCount` is the length of the suggestion list (F6.R12 "in N payments").
- **Covers:** F6.R1, F6.R2, F6.R3, F6.R4, F6.R11 (recomputation), F6.R12, F6.AC1, F6.AC7, F6.AC10, F6.AC14.
- **Tests:** unit (xUnit, FluentAssertions):
  - **Example 1:** balances Sam `+5000`, Alex `-1000`, Jo `-4000` give Jo pays Sam 4000, then Alex pays Sam 1000 (F6.AC1);
  - **Example 3:** after Jo's 4000 payment is applied, one suggestion is left, Alex pays Sam 1000 (F6.AC2);
  - **Tie:** Sam `+1000` (added first), Alex `+1000`, Jo `-2000` give Jo pays Sam 1000, then Jo pays Alex 1000 (F6.AC7);
  - **Tie on debtors:** two debtors of equal size pay the creditor in `seq` order;
  - zero balances give no suggestions and a `null` percentage with `owedCents` `0` (F6.AC14);
  - **99%:** paid back 19900 of 20000 gives `99`, not `100` (F6.AC10);
  - `100` only when `leftToSettleCents` is `0`;
  - **property:** for many random balance sets with a fixed seed, applying every suggestion brings every balance to `0`, the sum stays `0`, and the number of suggestions is at most the number of non-zero people;
  - the same input gives the same output on repeated runs;
  - large amounts do not overflow the percentage (`Int128`).
- **Done when:** the tests pass and no `double` or `float` appears in these files.

### F6.T3 — Core: payment rules, trip lock and services
- **Files:** `TripExpenseTracker.Core/Payments/Payment.cs`, `PaymentRules.cs`, `IPaymentDao.cs`, `PaymentService.cs`, `SettleUpService.cs`; `TripExpenseTracker.Core/IUnitOfWork.cs` (adds `LockTripAsync`); unit tests.
- **Change:**
  - `PaymentRules` refuses payer equal to payee (`payer_payee_same`) and an amount of 0 or less (`amount_not_positive`) (F6.R6).
  - `PaymentService.MarkPaidAsync(owner, tripId, payer, payee, amountCents)`: inside one unit of work, lock the trip (refuses foreign and missing trips as "not found"), load balances through F5's `BalanceService` logic, recompute suggestions with T2, and check that one suggestion has the same payer, payee and amount. If none does, return `suggestions_changed` and insert nothing (F6.R18). Otherwise insert the payment with `markedPaidAt` from `TimeProvider`.
  - `PaymentService.DeleteAsync(owner, tripId, paymentId)`: lock the trip, delete the owner's payment on that trip, and return not-found if nothing was deleted (F6.R8, F6.R15).
  - `SettleUpService.GetAsync(owner, tripId)`: balances, suggestions, payments newest first, progress and totals.
  - Every write that changes a payment takes the trip lock first.
- **Covers:** F6.R5, F6.R6, F6.R8, F6.R9, F6.R10, F6.R11, F6.R14, F6.R15, F6.R18, F6.AC4, F6.AC5, F6.AC6, F6.AC9, F6.AC13 (service side).
- **Tests:** unit with fake DAO and `FakeTimeProvider`:
  - marking Jo pays Sam 4000 when that is the current suggestion records it at the fake clock's time (F6.AC5);
  - a triple that is not a current suggestion returns `suggestions_changed` and inserts nothing (F6.AC9, F6.R18);
  - a payer equal to payee returns `payer_payee_same`;
  - delete removes the payment and not-found is returned for an unknown id (F6.AC6);
  - a foreign or missing trip returns "not found" for every call (F6.AC13);
  - every write calls the trip lock before reading or inserting (checked with a fake unit of work);
  - after marking all suggestions paid, the settle-up result shows every balance `0`, no suggestions, and the settled state (F6.AC3);
  - Cancel is a UI concern; the service records nothing unless called (F6.AC4 service side).
- **Done when:** the tests pass.

### F6.T4 — Data: `PaymentDao`, `TripLock` and `PaymentUsageSource`
- **Files:** `TripExpenseTracker.Data/PaymentDao.cs`, `TripLock.cs`, `PaymentUsageSource.cs`, DI registration.
- **Change:**
  - `TripLock.LockAsync(owner, tripId)`: `SELECT id FROM trips WHERE id = @trip AND owner_user_id = @owner FOR UPDATE` on the unit of work's transaction. Zero rows is not found.
  - `PaymentDao`: insert, list newest first by `seq`, and delete with the owner join. Delete returns the affected row count.
  - `PaymentUsageSource.GetPersonIdsAsync(tripId)`: payers and payees, filtered by `trip_id` (F3.R7).
- **Covers:** F6.R7, F6.R8, F6.R15, F6.R16 (in-use source), F6.AC6, F6.AC15, F6.AC16 (DAO side).
- **Tests:** none automated (unit tests only; the DAO is SQL). **Code review checklist:**
  - `TripLock` runs `SELECT ... FOR UPDATE` filtered by the owner, on the unit of work's transaction, and zero rows is "not found". This is what stops two simultaneous Mark as paid calls from recording the same suggestion twice (F6.R18);
  - list, insert and delete join `trips` and filter on the owner, so user B gets the same result as a missing trip (F6.AC13, F6.R15);
  - the list orders by `seq DESC` (F6.AC15); delete returns the affected row count;
  - `PaymentUsageSource` returns payers **and** payees, filtered by `trip_id` (F6.AC12);
  - parameters only, no string-concatenated SQL.
- **Done when:** the build passes, the checklist is ticked by a reviewer, and a manual run behaves as expected: mark, list, delete; and marking the same suggestion from two browser tabs records it once.
- _Needs human review: ownership and locking in SQL (no automated test covers them)._

### F6.T5 — Cross-feature changes: F2, F3, F4 and F5 code (touches approved plans)
- **Files:**
  - **F4:** `ExpenseService` and `ExpenseDao` add and delete paths take the trip lock (F4.T3, F4.T4).
  - **F5:** `BalanceDao` reads payments; `BalanceCalculator` uses the payment terms; the "Left to settle" tile shows "in {n} payments" (F5.T5, F5.T6).
  - **F3:** `PaymentUsageSource` is registered with the people service (`Program.cs`).
  - **F2:** `TripDao` detail returns the real `paymentCount` (F2.T4).
- **Change:**
  - F4 add and delete run inside a transaction that first takes the trip lock. The F4 behavior and every F4 assertion are unchanged. The lock is the only addition.
  - F5's formula now includes the payment sums. Every F5 test still passes, because F5's tests have no payments.
  - F5's "Left to settle" tile on the Expenses view gets the "in {n} payments" text, with the plural rule ("in 1 payment", "in 0 payments").
  - The payment usage source is registered, so a person in a payment shows "In payments" and can't be removed (F3's people service already handles any registered source).
- **Covers:** F5.R1 (payment terms), F5.R8 ("in N payments"), F5.R7 (updates on payment change), F3.R7 (real payment data), F2.R7 (paymentCount), F6.R10, F6.R16.
- **Tests:** unit:
  - `BalanceCalculator` with payments: a payment raises the payer's balance and lowers the payee's by the same amount, and the total stays `0` (F6.R10, F5.AC2);
  - **Example 3** through `BalanceCalculator`: after Jo's 40.00 payment, Sam is owed 10.00, Alex owes 10.00, Jo is settled;
  - random expense and payment sets (fixed seed) still sum to `0`;
  - `ExpenseService` add and delete call the trip lock before writing (fake unit of work), and F4's existing unit tests pass unchanged.
- **Review:** `BalanceDao` reads payments inside the owner-filtered query; the detail's `paymentCount` is inside the owner-filtered query; the usage source is registered. **Manual check:** after marking a payment, the payer's People row shows "In payments" and removal is refused; the delete dialog shows "1 payment".
- **Done when:** the tests pass and the existing F2–F5 tests pass with no assertion edited or removed.
- _Needs human review: adds a lock to approved F4 code and changes F2, F3 and F5 code (project rules)._

### F6.T6 — Api: `PaymentsController` and `SettleUpController`
- **Files:** `TripExpenseTracker.Api/Controllers/PaymentsController.cs`, `SettleUpController.cs`, request and response records; DI wiring; `TripExpenseTracker.Tests/Api/PaymentsControllerTests.cs`, `SettleUpControllerTests.cs`.
- **Change:** the three endpoints in Contract changes. The default policy applies with no `[AllowAnonymous]`. The owner comes only from `ICurrentUser`. The amount is integer cents from the client, which is what the user saw (Contract changes). Results map to the codes in Contract changes.
- **Covers:** F6.R1, F6.R5, F6.R6, F6.R7, F6.R8, F6.R9, F6.R12, F6.R14, F6.R15, F6.R16, F6.R17 (through the API), F6.AC1, F6.AC2, F6.AC3, F6.AC5, F6.AC6, F6.AC8, F6.AC9, F6.AC10, F6.AC11 (no update route), F6.AC13, F6.AC14, F6.AC15, F6.AC16.
- **Tests:** unit (xUnit, the controllers built directly with fake services and a fake `ICurrentUser`, no web host or database):
  - the settle-up result maps to the response with suggestions in the order given, payments, `paidBackCents`, `owedCents`, `leftToSettleCents`, `suggestionCount`, `percentage` (including `null`) and the totals (F6.AC1, F6.AC8, F6.AC10, F6.AC14);
  - a recorded payment returns `201` with its fields (F6.AC5);
  - `suggestions_changed` maps to `409` (F6.AC9); `payer_payee_same`, `amount_not_positive`, `payer_not_on_trip` and `payee_not_on_trip` map to `400` with those codes;
  - delete returns `204`; a missing payment and a foreign trip map to the same `404` body (F6.AC6, F6.AC13, F6.R15);
  - the owner id is always the fake `ICurrentUser`'s, and the client's amount is passed through as integer cents.
- **Review:** no `[AllowAnonymous]`, so F1's default-deny policy returns `401` when logged out; no `PUT` or `PATCH` actions exist (F6.AC11, F6.R9).
- **Done when:** the tests pass and no suggestion, percentage or payment amount is computed or stored from the client.
- _Needs human review: authorization and ownership checks._

### F6.T7 — Front end: the view switch, Settle up view and tiles
- **Files:** `app/src/web/src/app/settle-up/settle-up.service.ts`, `trip-view-switch/trip-view-switch.component.{ts,html,scss}`, `settle-up-view/settle-up-view.component.{ts,html,scss}`, `summary-tiles-settle/summary-tiles-settle.component.{ts,html,scss}`, `pluralize.ts`, specs; `trips/trip-page/trip-page.component.{ts,html}` (view switch); `balances/summary-tiles/` (F5.T6, "in N payments").
- **Change:**
  - The trip page has a view switch labelled "Trip view" with two buttons, **Expenses** and **Settle up**. Each has `aria-pressed` for its state (Accessibility). The default is Expenses (F6 spec, User flow step 1).
  - The Settle up view has three tiles in this order (F6.R12, mockup, final labels):
    - **Paid back so far**: the amount, with "of {owed} owed" beneath it.
    - A progress bar (`progressbar`, labelled "Paid back so far") with the percentage in text. When `owedCents` is `0`, the bar is hidden and "Nothing to settle yet." replaces the percentage (States table).
    - **Left to settle**: the amount, with "in {n} payments" beneath it, using the plural rule ("in 1 payment", "in 0 payments", "in N payments").
    - **Total spent**: the amount, with "across {n} expenses" beneath it, using the plural rule ("across 1 expense", "across N expenses").
  - The Balances panel from F5 is shown in this view, unchanged (F6.R13).
  - The Expenses view's "Left to settle" tile from F5 shows "in {n} payments" too (F5.R8).
  - The percentage is text, so the progress bar's meaning is carried by the number (Accessibility).
  - Amounts use `money.ts` from F4 and F5 with two decimals and thousands separators.
  - The tiles are headings with their numbers as text, so a screen reader reads "Paid back so far, 40.00 of 50.00 owed".
- **Covers:** F6.R12, F6.R13, F6.AC8, F6.AC10, F6.AC14 (tiles side), F6.AC3 (tiles side); F5.R8 (count text).
- **Tests:** front end:
  - the switch shows Expenses by default, and selecting **Settle up** shows the Settle up view with its three tiles in order;
  - Example 1 after Jo's payment: "40.00 of 50.00 owed", a bar at 80%, and "10.00" under Left to settle with "in 1 payment" (F6.AC8);
  - 199.00 of 200.00 shows "99%" (F6.AC10);
  - zero owed hides the bar and shows "Nothing to settle yet." (F6.AC14);
  - the plural forms: "in 1 payment", "in 0 payments", "in 2 payments", "across 1 expense";
  - the Balances panel appears in the Settle up view and is the same component (F6.R13);
  - the Expenses view's Left to settle tile reads "in 2 payments" when two are recorded.
- **Done when:** the tests pass and every label matches the mockup text.

### F6.T8 — Front end: Suggested payments panel
- **Files:** `settle-up/suggested-payments-panel/suggested-payments-panel.component.{ts,html,scss}`, spec.
- **Change:**
  - The panel heading is "Suggested payments", with the helper texts from the mockup: "The fewest payments that bring every balance to zero." and "Money moves outside the app. Mark a payment as paid once it's been made."
  - Each suggestion is a row: "{payer} pays {payee}", "Settles {payer}'s balance", the amount, and a **Mark as paid** button. The button's accessible name is "Mark {payer} pays {payee} {amount} as paid" (Accessibility).
  - The list is a real `<ul>`, in the order returned.
  - With no suggestions (every balance zero), the panel shows "Everyone is settled up." with a decorative check mark, and the Left to settle tile shows 0.00 in 0 payments (F6.R14).
  - Loading shows "Loading settle up…"; the error shows "Something went wrong. Please try again." in the panel.
  - A `404` makes the trip page show "This trip no longer exists." once.
- **Covers:** F6.R1, F6.R2, F6.R5 (button), F6.R14, F6.AC1, F6.AC3, F6.AC14 (panel side); spec, Accessibility.
- **Tests:** front end (Angular Testing Library, mocked HTTP):
  - Example 1: the rows are "Jo pays Sam" 40.00 then "Alex pays Sam" 10.00 (F6.AC1);
  - the button's accessible name is "Mark Alex pays Sam 10.00 as paid";
  - no suggestions shows "Everyone is settled up.";
  - the helper texts appear as written;
  - the loading and error states show the copy;
  - `404` hands over to the trip page.
- **Done when:** the tests pass and the text matches the spec word for word.

### F6.T9 — Front end: mark-as-paid and delete-payment dialogs, and messages
- **Files:** `settle-up/mark-payment-dialog/mark-payment-dialog.component.{ts,html,scss}`, `settle-up/delete-payment-dialog/delete-payment-dialog.component.{ts,html,scss}`, specs; `suggested-payments-panel` and `payments-made-list` (wiring).
- **Change:**
  - **Mark as paid:** a CDK dialog, built like F2's delete dialog. Title "Mark this payment as paid?", body "{payer} pays {payee} {amount}. This records that the money has moved outside the app.", buttons **Mark as paid** and **Cancel** (mockup `F3, F4, F6 · Dialogs and messages.png`, bottom-left). Initial focus goes to **Cancel**.
  - **Cancel** records nothing and the suggestion stays. **Mark as paid** sends the payer, payee and amount the user saw (`amountCents`).
  - On success the dialog closes, "Payment recorded." shows in a `role="status"` region, and the suggestions, tiles and balances reload (F6.AC5).
  - On `409` `suggestions_changed` the dialog closes, the suggestions reload, and "The suggested payments have changed. Review them and try again." shows as a message in the Suggested payments panel. Nothing is recorded (F6.AC9). The plan chooses the panel for this message, which the spec allows.
  - **Delete payment:** a CDK dialog. Title "Delete this payment?", body "{payer} paid {payee} {amount}. Deleting it puts the amount back into the balances.", buttons **Delete payment** and **Cancel** (mockup, bottom-middle). Initial focus goes to **Cancel**.
  - **Cancel** keeps the payment. **Delete payment** sends `DELETE`. On success "Payment deleted." shows and the lists and tiles reload. On `404` "This payment no longer exists." shows in the Payments made list.
  - Both dialogs trap focus. Escape closes them and returns focus to the button that opened them (Accessibility).
  - Other errors show "Something went wrong. Please try again." inside the dialog.
- **Covers:** F6.R5, F6.R6, F6.R8, F6.R18, F6.AC4, F6.AC5, F6.AC6, F6.AC9; spec, Accessibility.
- **Tests:** front end:
  - the mark dialog shows the exact title, body and buttons with the names and amount;
  - Cancel sends no request and records nothing (F6.AC4);
  - Mark as paid sends the payer, payee and `amountCents` and shows "Payment recorded.";
  - a `409` closes the dialog and shows the stale message in the panel, then reloads the suggestions (F6.AC9);
  - the delete dialog shows the exact title, body and buttons; Cancel keeps the row (F6.AC6);
  - Delete payment shows "Payment deleted."; a `404` shows "This payment no longer exists.";
  - Escape closes each dialog and focus returns to its opener; focus stays inside while open.
- **Done when:** the tests pass and all copy matches the spec and the mockup.

### F6.T10 — Front end: Payments made list
- **Files:** `settle-up/payments-made-list/payments-made-list.component.{ts,html,scss}`, `settle-up/date-format.ts`, specs.
- **Change:**
  - Heading "Payments made" with "{n} payments" (plural rule as in F6.T7).
  - A real `<ul>`, newest first (F6.R7, F6.AC15). Each row shows "{payer} paid {payee}", the amount, "Marked paid · {date}", and a delete control.
  - The date is the full day, month and year in the browser's local time zone, for example "20 Oct 2026" (spec, Edge cases). `date-format.ts` converts the UTC timestamp for display only.
  - The delete control's accessible name is "Delete {payer} paid {payee} {amount}" (Accessibility).
  - There is no edit control (F6.AC11, F6.R9).
  - Empty state: "No payments yet. Payments you mark as paid will appear here." (States table).
- **Covers:** F6.R7, F6.R9, F6.AC5, F6.AC11, F6.AC15.
- **Tests:** front end:
  - rows show "Jo paid Sam", 40.00 and "Marked paid · 20 Oct 2026" for a payment in UTC on that date, in a fixed time zone set in the test;
  - newest first;
  - the delete control's accessible name includes the payment;
  - no edit control;
  - the empty state copy;
  - "1 payment" and "2 payments" in the heading.
- **Done when:** the tests pass.

### F6.T12 — Trip list: settle status on each card (touches F2 and F5 code)
- **Files:** `TripExpenseTracker.Core/Balances/TripStatusRules.cs` (new), `TripExpenseTracker.Data/BalanceDao.cs` (owner-wide read), `TripExpenseTracker.Api/Controllers/TripsController.cs` (list response, F2), `app/src/web/src/app/trips/trip-list/` (card badge), specs; `TripExpenseTracker.Tests/Api/TripsControllerTests.cs` (new assertions only); the F5.T7 card check.
- **Change:**
  - `TripStatus.From(expenseCount, suggestionCount)`:
    - no expenses → `none`, no badge (the mockup's "Mom's 60th in Cebu" card has people but no expenses and no badge);
    - suggestions greater than 0 → `toSettle`, with the count;
    - no suggestions, with expenses → `settled`.
  - One owner-scoped read (`BalanceDao.ListForOwnerAsync`) fetches the balances of all the user's trips, so the list does not run one query per card (at most 50 trips). Suggestions for each trip come from T2's rule.
  - `GET /api/trips` gains a field on each item: `settle: { status: "none" | "toSettle" | "settled", paymentsToSettle: int }`. The change is additive, and existing fields and their order are unchanged.
  - Card badges (mockup, final): "N payments to settle" in the amber style and "Settled up" in the green style with a check mark. The words carry the meaning, so colour is not the only signal.
  - The card keeps its link to the trip page, and it still has no Delete control on the card (F2 spec, Scope).
- **Covers:** F6.R19, F6.R20, F5.R10 (the status, added with F6), F6.AC17, F6.AC18, F6.AC19, F6.AC20, F6.AC21. F2.R5 (list order and ownership) must still hold.
- **Tests:**
  - unit: the status table: no expenses gives `none`; one or more suggestions gives `toSettle` with that count; no suggestions with expenses gives `settled`; Example 1 gives `toSettle` with 2;
  - unit (service and Api): the trip-list service computes each card's `paymentsToSettle` with the same T2 rule the settle-up view uses, from one owner-wide read (a counting fake `IBalanceDao` shows one call per list request, not one per trip); the list item carries the `settle` field;
  - review: the owner-wide read filters by the owner, so another user's trips are not included (F6.AC21); the list order is unchanged;
  - front end: a card with `toSettle` 2 shows "2 payments to settle" (F6.AC17); a card with `toSettle` 1 shows "1 payment to settle" (F6.AC18); a `settled` card shows "Settled up" (F6.AC19); a `none` card shows no badge (F6.AC20); the card has no Delete control (F6.R20);
  - F5.T7 check: the F5 card test (F5.AC13, "no balance status") is retired and replaced by the F6.AC17–AC21 checks above. Approved 2026-10-21.
- **Done when:** the tests pass, and the F2 list tests pass with only the new assertions added.
- _Needs human review: changes an F5 test that the spec wrote for F5 only, and adds a field to an F2 response._

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F6.R1 | T2, T6, T8 | AC1 | T2 Example 1; T6 mapping; T8 rows |
| F6.R2 | T2, T8 | AC1, AC2 | T2 rule and Example 3 |
| F6.R3 | T2 | AC7 | T2 ties |
| F6.R4 | T2 | AC1 | T2 follows R6 as written, no search for the minimum |
| F6.R5 | T3, T6, T9 | AC4, AC5 | T3 mark; T6 `201`; T9 dialog |
| F6.R6 | T1, T3, T6 | AC5 | T1 review (check and composite key); T3 rules; T6 `400` codes |
| F6.R7 | T4, T10 | AC5, AC15 | T4 review (`seq DESC`); T10 list |
| F6.R8 | T3, T4, T6, T9 | AC6 | T3 delete; T6 `204` and `404`; T9 dialog |
| F6.R9 | T1, T6, T10 | AC11 | T6 review (no `PUT`/`PATCH`); T10 no edit control |
| F6.R10 | T5 | AC2 | T5 payment terms in the F5 formula; total stays `0` |
| F6.R11 | T2, T5 | AC2 | T2 recomputation; T5 Example 3 balances |
| F6.R12 | T2, T6, T7 | AC8, AC10, AC14 | T2 progress and count; T6 response mapping; T7 tiles |
| F6.R13 | T7 | none | T7 Balances panel in the Settle up view |
| F6.R14 | T3, T6, T8 | AC3, AC14 | T3 all paid; T6 settled mapping; T8 panel |
| F6.R15 | T3, T4, T6 | AC13 | T3 foreign trip; T4 review (owner join); T6 identical `404` |
| F6.R16 | T4, T5, T6 | AC12 | T4 review (usage source); T5 manual check; T6 `payer_not_on_trip` mapping |
| F6.R17 | T1 | AC16 | T1 review (cascade); manual check |
| F6.R18 | T2, T3, T4, T6, T9 | AC9 | T3 stale check; T4 review (trip lock); T6 `409`; T9 message |
| F6.R19 | T12 | AC17, AC18, AC19, AC20 | T12 status table; card front-end tests |
| F6.R20 | T12 | AC21 | T12 review (owner-only read); no Delete control on the card |
| F5.R10 (added with F6) | T12 | F6.AC17–AC21 | as above |

| Acceptance criterion | Tasks | Test |
|---|---|---|
| F6.AC1 | T2, T6, T8 | T2 Example 1; T6 mapping; T8 rows |
| F6.AC2 | T2, T5 | T2 Example 3; T5 Example 3 balances |
| F6.AC3 | T3, T6, T8 | T3 all paid; T6 settled; T8 panel |
| F6.AC4 | T3, T9 | T3 service records nothing unless called; T9 Cancel |
| F6.AC5 | T3, T6, T9, T10 | T3 fake clock; T6 `201`; T9 message; T10 row |
| F6.AC6 | T3, T6, T9 | T3 delete; T6 `204`/`404`; T9 Cancel |
| F6.AC7 | T2 | T2 tie |
| F6.AC8 | T2, T7 | T2 progress; T7 tiles |
| F6.AC9 | T3, T4, T6, T9 | T3 stale check; T4 review and two-tab manual check; T6 `409`; T9 message |
| F6.AC10 | T2, T6, T7 | T2 99%; T6 mapping; T7 tile |
| F6.AC11 | T6, T10 | T6 review (no `PUT`); T10 no edit |
| F6.AC12 | T1, T4, T5 | T1 and T4 review; T5 manual check ("In payments", removal refused) |
| F6.AC13 | T3, T6 | T3 foreign trip; T6 identical `404`; manual check with two users |
| F6.AC14 | T2, T6, T7, T8 | T2 zero owed `null`; T6 mapping; T7 text; T8 panel |
| F6.AC15 | T4, T10 | T4 review; T10 newest first |
| F6.AC16 | T1 | T1 review (cascade); manual check |
| F6.AC17 | T12 | T12 "2 payments to settle" |
| F6.AC18 | T12 | T12 "1 payment to settle" |
| F6.AC19 | T12 | T12 "Settled up" |
| F6.AC20 | T12 | T12 no badge for a trip with no expenses |
| F6.AC21 | T12 | T12 review (owner-only read) |

## Test plan
Unit tests only (project rules).
- **Unit (xUnit, Core):** the suggestion rule, ties and the recompute property (T2); the percentage and its `Int128` safety (T2); payment rules, the stale check, the trip lock order and the settle-up result with a fake DAO (T3); the F5 formula with payments, Example 3 and the F4 lock call (T5); the card status table and the single owner-wide read (T12).
- **Unit (xUnit, Api):** both controllers' result mapping, every code, the identical `404`s and the owner always taken from `ICurrentUser` (T6, T12).
- **Front end (Angular Testing Library, mocked HTTP):** the view switch, tiles and plurals (T7); the suggestions panel (T8); the dialogs, focus and messages (T9); the payments list and dates (T10); the card badges (T12).
- **Code review (not testable with unit tests):** T1 migration (checks, composite keys, `NO ACTION`, cascade); T4 checklist (the trip lock that prevents double recording, owner joins, order, usage source); T5 and T12 owner-filtered reads.
- **Existing suites:** F1's integration and Playwright tests must still pass. Run them once at the end of F6.
- **Worked examples:**
  - **Example 1:** the suggestions Jo pays Sam 40.00 and Alex pays Sam 10.00. Named tests: T2, T6, T8 (F6.AC1).
  - **Example 2:** the balances are covered in F5. Named test: F5.T1.
  - **Example 3:** after Jo's payment, one suggestion (Alex pays Sam 10.00) is left. Named tests: T2 (F6.AC2) and T5 (the balances).
- **Manual checks:** phone-width layout of the tiles, the progress bar, the suggestion rows and the dialogs; keyboard-only through the view switch, Mark as paid, Cancel, and Delete payment; a screen reader reading the progress bar, the "Mark … as paid" names and the payments list; 4.5:1 contrast on the status text and the stale message; focus trapped in the dialogs and returned on close.

## Risks & mitigations
- **No automated test covers the SQL or the race.** The trip lock is what stops two simultaneous Mark as paid calls from recording one suggestion twice (F6.R18). Without integration tests, it is verified only by the T4 review and a two-tab manual check. T4 is marked for human review. The money rules (suggestions, percentage, balances with payments) stay fully unit-tested in Core.
- **F1–F5 are not built (blocker).** F6 starts only after F5's tasks T1–T7 exist, and F4's and F3's tasks as listed. If their names change, update F6's tasks.
- **F4 changes (T5).** The trip lock is added to F4's approved add and delete paths. T5 is marked for human review. If you prefer not to change F4, the other option is to lock only the payment writes, which weakens F6.R18. You chose the lock.
- **The suggestion rule is not always minimal.** product.md Goals ask for "as few payments as possible". R6, which the spec follows (F6.R4), can sometimes give one more payment. The plan keeps R6 as written, and the spec records that decision.
- **Suggestions are computed, not stored.** This keeps them always correct. Each request recomputes them under the lock. If trips grow large, a cache would need clearing on every payment and expense change.
- **Stale message placement.** The spec allows a dialog or a panel message. The plan puts the message in the panel, after the dialog closes. Confirm it when the design is reviewed.
- **Copy the spec does not give:**
  - the link or button label for a dismissed stale message (none needed, since it is a message);
  - the "Settles {payer}'s balance" line in each suggestion comes from the mockup, so it is final, as the spec says;
  - the wording of the Payments made heading when the count is zero (the plan shows "Payments made" with no count).
- **Date display.** The date is shown in the browser's local time zone, as the spec says. Tests fix a time zone so they do not depend on the machine.
- **Card status is in the F6 spec.** It is F6.R19–R20 and F6.AC17–AC21, and T12 cites them, with F5.R10 as the source.
- **F5.AC13 and the card status conflict.** F5.AC13 says cards show no balance status. It is worded for F5 only (F5.R10 says "in F5"), and T12 makes it false after F6. Settled on 2026-10-21: the F5.AC13 card test is F5 only, and T12 retires it in favour of F6.AC17–AC21.
- **Card totals, people counts and expense counts are in F3 and F4.** F3.T10 adds the people count and F4.T11 adds the expense count and total, both approved. F6 does not cover them.
- **Singular wording.** "1 payment to settle" is decided (F6.R19, decided 2026-10-21).
- **No badge for a trip with no expenses.** Decided 2026-10-21 (F6.R19, F6.AC20), following the mockup's "Mom's 60th in Cebu" card.
- **Open questions in the spec:** none.

## Out of scope
- Partial payments, recording payments that were not suggested, and editing a payment (spec, Out).
- Money moving through the app, currency, notifications and reminders (spec, Out and product.md).
- The optimal "fewest payments" search, which product.md Goals mention and R6 does not do (F6.R4).
- Totals, people counts and expense counts on trip list cards. Covered by F3.T10 and F4.T11, not F6 (see Risks).

## Decisions log
- 2026-10-21: _(Decided by you, 2026-10-21.)_ Concurrency: lock the trip for every money write (mark, delete payment, add and delete expense), so a suggestion is never checked against changing balances.
- 2026-10-21: _(Proposed in the plan, not asked; no product behavior changes.)_ Suggestions are computed on each request, not stored.
- 2026-10-21: _(Proposed.)_ Percentage uses `Int128` for the multiplication, and returns `null` when nothing is owed.
- 2026-10-21: _(Proposed.)_ Payments have a database check that payer and payee differ, plus composite keys to `people` for same-trip and `NO ACTION` for removal blocking.
- 2026-10-21: _(Proposed.)_ The stale-suggestion message appears in the Suggested payments panel after the dialog closes.
- 2026-10-21: _(Proposed.)_ The Mark as paid request carries the amount in integer cents, which is what the user saw.
- 2026-10-21: _(Proposed.)_ The payment-level `404` and a foreign trip's `404` share one response body, so the two are indistinguishable (F6.R15).
- 2026-10-21: _(Decided by you.)_ Trip-list balance status (settled up, payments to settle) is added in F6.T12, citing F5.R10.
- 2026-10-21: The card status is now in the F6 spec as F6.R19–R20 and F6.AC17–AC21. T12 cites them. The plan is Draft until you approve it.
- 2026-10-21: F5.AC13 settled as F5 only. You approved retiring the F5 card test in F6.T12, which replaces it with F6.AC17–AC21.
- 2026-10-22: Test scope reduced to unit tests only, following the project rules change. T1 and T4 lose their integration tests and get review checklists and manual checks instead; T5's integration tests become Core unit tests (the F5 formula with payments, Example 3, the F4 lock call) plus a review; T6's endpoint tests become controller unit tests with fakes; T12's integration assertions become unit tests with a counting fake and a review; F6.T11 (Playwright end-to-end) is removed and its ID is not reused. F3 no longer has a payment stand-in test to replace. F1's existing integration and e2e tests stay and must pass. Status set back to Draft for re-approval.
- 2026-10-22: Approve the plan with unit tests only? → Yes. Status set to Approved.
