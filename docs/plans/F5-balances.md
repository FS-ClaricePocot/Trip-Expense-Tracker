# F5 — Balances: Implementation plan

_Status: Approved · Last updated: 2026-10-21 · Spec: [F5-balances](../specs/features/F5-balances.md) (Approved, last updated 2026-10-20)_

## Overview
F5 shows what each person on a trip is owed or owes, and checks that all balances add up to exactly zero. It adds no tables. Balances are calculated on every request from the trip's expenses (F4), with the payments term fixed at zero until F6 adds payments. It adds the balances endpoints, the Balances panel beside the Expenses panel, and the two summary tiles on the trip page. F5 is the fifth build step (F1 → F6), and F6 reuses the Balances panel unchanged.

## Prerequisites
- **Dependencies:**
  - **F1: planned, not built (blocking).** Reused: `ICurrentUser`, the default-deny policy, the ProblemDetails middleware, `PostgresFixture`, `FakeTimeProvider`, the HTTP interceptor and `problem-mapper.ts`.
  - **F2: planned, not built (blocking).** Reused: `ITripDao.GetAsync`, the trip page, the owner-checked "not found" pattern.
  - **F3: planned, not built (blocking).** Reused: the `people` table and its `seq` order (F3.T1), `PersonDao` (F3.T3), and the people list service (F3.T6).
  - **F4: planned, not built (blocking).** Reused: `ExpenseShares` in Core (F4.T2), `ExpenseDao` list query with participants and `seq` (F4.T4), and the Expenses panel on the trip page (F4.T7).
  - F6: not started. F6 depends on F5 and adds the payments term.
- **Other features' code:** F5.T5 changes the trip page that F2 and F4 own, to add the summary tiles and the Balances panel. It is a layout change with tests, and it is listed under Contract changes.
- **Setup:** none beyond the earlier features'.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | none | F5 stores nothing |
| `TripExpenseTracker.Core` | `Balances/BalanceCalculator.cs`, `Balances/PersonBalance.cs`, `Balances/BalanceSummary.cs`, `Balances/BalanceCheck.cs`, `Balances/IBalanceDao.cs`, `Balances/BalanceService.cs` | Pure formula, the zero-sum check, Left to settle and Total spent, orchestration |
| `TripExpenseTracker.Data` | `BalanceDao.cs` | One read of the trip's people and expenses, owner-checked |
| `TripExpenseTracker.Api` | `Controllers/BalancesController.cs`, response records, `Program.cs` (logging) | `GET` balances and `GET` summary |
| Angular (`app/src/web/src/app/balances/`) | `balances.service.ts`, `balances-panel/`, `balance-tag.ts` (display rules), `money.ts` (formatting, shared with F4), `summary-tiles/` | Balances panel, tags, bars, footer, tiles |
| Trip page | `app/src/web/src/app/trips/trip-page/` (F2, F4) | Adds the tiles above and the Balances panel beside Expenses |
| Tests | `TripExpenseTracker.Tests`, `TripExpenseTracker.IntegrationTests`, web specs, `app/e2e` | Unit, integration, component and Playwright |

Rules from `CLAUDE.md` and `project-rules.md` that are easy to break here:
- **Money is `long` cents.** The formula, the check and the totals are all integer arithmetic in Core. Formatting to "1,842.50" happens in `money.ts` for display only.
- **Bars use a fraction, not money.** The bar width is a CSS percentage computed from the cents. It is layout only and never stored or summed.
- **Ownership in every query.** `BalanceDao` filters on the owner, and foreign and missing trips return the same `404` (F5.R12).
- **Nothing sensitive in logs.** When the zero-sum check fails, the log line carries the trip id and the number of people only, never the amounts or names (F5.AC16 proposal).
- **Time from `TimeProvider`.** Balances do not depend on time, but no test may read the real clock.

## Contract changes
- **Database:** none. F5 creates no tables and no columns. Balances are calculated from `people`, `expenses` and `expense_participants` (F3, F4).
- **Balance formula (Core, `BalanceCalculator`):** for each person, `balance = paid − sum(shares) + paymentsMade − paymentsReceived`. In F5 the payment terms are `0`, because the `payments` table arrives with F6. `paid` is the sum of `amountCents` of expenses where the person is the payer. `shares` come from F4's `ExpenseShares`, using the participants' `seq` order. The formula is written with all four terms now, so F6 only supplies the payment sums and does not change the calculation (F5.R1).
- **Status (Core):** positive → `owed`, negative → `owes`, zero → `settled`. The API returns the status and the cents; the front end chooses the words "is owed", "owes" and "settled up" (F5.R6). The sign is never shown.
- **Zero-sum check (Core, `BalanceCheck`):** the sum of all balances must equal `0`. If it does not, the service returns `balances_inconsistent` and the balances are not returned (F5.R3, F5.AC16).
- **Summary (Core):**
  - `totalSpentCents` = sum of all expense amounts (F4.R13).
  - `leftToSettleCents` = sum of all positive balances (F5.R8).
- **No caching.** The results are calculated on each request. A cache would have to be cleared on every expense change (F5.R7), which is more risk than the cost of the calculation for a trip of this size. The plan records this as a decision.
- **API** (JSON, all need a session through F1's default policy; the owner comes from `ICurrentUser`):
  | Method and path | Request | Success | Failure |
  |---|---|---|---|
  | `GET /api/trips/{tripId:guid}/balances` | none | `200` `{ totalCents: 0, people: [{ id, name, balanceCents, status }] }` in `seq` order, with `status` one of `owed`, `owes`, `settled`. A zero balance is still listed (F5.R4). | `404` (identical for foreign, missing or non-GUID trip). `500` ProblemDetails `balances_inconsistent` when the zero-sum check fails, with no amounts in the body. `401`. |
  | `GET /api/trips/{tripId:guid}/summary` | none | `200` `{ totalSpentCents, leftToSettleCents }` (F5.R8) | `404` as above. `401`. |
  - Neither endpoint accepts a balance or any other money value from the client (F5.R11).
  - The trip detail (F2) is unchanged. The summary is a separate request.
- **Trip list (F2):** unchanged. Cards show no balance status (F5.R10, F5.AC13).
- **Browser storage:** none.

## Tasks

### F5.T1 — Core: balance formula, zero-sum check and summary
- **Files:** `TripExpenseTracker.Core/Balances/BalanceCalculator.cs`, `PersonBalance.cs`, `BalanceSummary.cs`, `BalanceCheck.cs`; unit tests.
- **Change:** pure functions over plain inputs.
  - `BalanceCalculator.Calculate(people, expenses, payments)` where `people` are `(id, name, seq)`, `expenses` carry an amount, a payer and participants with `seq`, and `payments` is an empty list in F5. It uses F4's `ExpenseShares` for the shares, so the leftover-cent rule is not repeated. It returns one balance per person in `seq` order, with a zero balance included.
  - `BalanceCheck.Verify(balances)` returns `balances_inconsistent` unless the sum is exactly `0`.
  - `BalanceSummary.Compute` returns `totalSpentCents` and `leftToSettleCents` (sum of positive balances).
  - Status is derived from the sign (Contract changes).
- **Covers:** F5.R1, F5.R2, F5.R3, F5.R4, F5.R8, F5.AC1, F5.AC2, F5.AC3, F5.AC5, F5.AC6, F5.AC8, F5.AC9, F5.AC10, F5.AC14, F5.AC16 (logic).
- **Tests:** unit (xUnit, FluentAssertions):
  - **Example 1:** Sam paid 90.00, Alex paid 30.00, all three share both. Sam `+5000` (`owed`), Alex `-1000` (`owes`), Jo `-4000` (`owes`), total `0`. Left to settle is `5000`, which is the sum of the positive balances and equals the sum of the amounts owed (Alex 10.00 and Jo 40.00) (F5.AC9);
  - **Example 2:** Sam paid 10.00, shared by Sam, Alex and Jo in that order: Sam `+666`, Alex `-333`, Jo `-333`, total `0` (F5.AC5);
  - **payer not a participant (F5.AC14):** Sam paid 30.00 for Alex and Jo: Sam `+3000`, Alex `-1500`, Jo `-1500`;
  - a person in no expense is `0` and `settled`, and is still listed (F5.AC3, F5.AC8);
  - the sum of balances is `0` for many random expense sets with a fixed seed (F5.AC2, F5.R3);
  - the check refuses a non-zero sum and returns `balances_inconsistent` (F5.AC16);
  - `Left to settle` is `0` when every balance is zero (F5.AC8);
  - `Total spent` for Example 1 is `12000` (F5.AC10);
  - the list is in `seq` order even when people were added in a different order (F5.AC6);
  - a balance of `0` never reports `owes 0.00` (status `settled`, F5.R6 Edge case).
- **Done when:** the tests pass and `Core` has no reference to `Data`, Npgsql or ASP.NET. No `double` or `float` appears in these files.

### F5.T2 — Data: `BalanceDao`
- **Files:** `TripExpenseTracker.Data/BalanceDao.cs`, DI registration; `IntegrationTests/Data/BalanceDaoTests.cs`.
- **Change:** one owner-checked read per request. It returns the trip's people in `seq` order, and its expenses with payer, amount, and participants with their `seq`. Every query joins `trips` and filters `owner_user_id = @owner`. The query shape is shared with F4's list where it fits, so the participant ordering is defined once.
- **Covers:** F5.R11, F5.R12 (DAO side), F5.AC12.
- **Tests:** integration (Testcontainers):
  - a trip with no people returns nothing and no error;
  - people come back in `seq` order, with a re-added person last;
  - expenses come back with participants in `seq` order;
  - user B's read of A's trip returns the same empty result as a missing trip (F5.AC12);
  - a read of a trip after an expense is deleted no longer includes it.
- **Done when:** the tests pass and a search of `BalanceDao.cs` finds no query without the owner join and no string-concatenated SQL.

### F5.T3 — Core: `BalanceService`
- **Files:** `TripExpenseTracker.Core/Balances/IBalanceDao.cs`, `BalanceService.cs`, `BalanceResults.cs`; unit tests.
- **Change:** `GetBalancesAsync(owner, tripId)` reads through `IBalanceDao`, runs T1, and returns the check result. `GetSummaryAsync(owner, tripId)` returns the T1 summary. A missing or foreign trip gives "not found" from both. Nothing is cached (Contract changes).
- **Covers:** F5.R7, F5.R11, F5.R12, F5.AC7 (service side), F5.AC12 (service side).
- **Tests:** unit with a fake DAO:
  - adding then deleting an expense, read through the service twice, returns the earlier balances (F5.AC7);
  - a foreign or missing trip returns "not found" for both calls;
  - a forced inconsistency (a fake DAO returns data that does not sum to `0`) returns `balances_inconsistent`, not the balances (F5.AC16).
- **Done when:** the tests pass.

### F5.T4 — Api: `BalancesController`
- **Files:** `TripExpenseTracker.Api/Controllers/BalancesController.cs`, response records; `Program.cs` (no logging of amounts); `IntegrationTests/Api/BalancesEndpointTests.cs`.
- **Change:** the two endpoints in Contract changes. The owner comes only from `ICurrentUser`. Results map to the codes in Contract changes. The `balances_inconsistent` log line records the trip id and the number of people only (F5.AC16 proposal, CLAUDE.md logging rule).
- **Covers:** F5.R1–R4, F5.R8, F5.R11, F5.R12, F5.AC1, F5.AC2, F5.AC3, F5.AC5, F5.AC6, F5.AC7, F5.AC8, F5.AC9, F5.AC10, F5.AC12, F5.AC16, F5.AC14.
- **Tests:** integration (`WebApplicationFactory` + Testcontainers, `FakeTimeProvider`, users through F1, trips and people through F2 and F3, expenses through F4):
  - Example 1 end to end: `GET /balances` shows Sam `+5000` `owed`, Alex `-1000` `owes`, Jo `-4000` `owes`, and `totalCents` `0` (F5.AC1, F5.AC2);
  - Example 2 end to end: Sam `+666`, Alex `-333`, Jo `-333` (F5.AC5);
  - the summary for Example 1 returns `totalSpentCents` `12000` and `leftToSettleCents` `5000` (F5.AC9, F5.AC10);
  - adding an expense changes the balances; deleting it returns them to the earlier values (F5.AC7);
  - a trip with people and no expenses shows every person `settled` and `leftToSettleCents` `0` (F5.AC8);
  - user B gets a `404` with the same status, `type` and body as a missing trip, for both endpoints (F5.AC12);
  - the forced inconsistency returns `500` `balances_inconsistent` with no amounts or names in the body, and the log output contains no amounts (F5.AC16);
  - the response has no field that accepts input, and `PUT` or `PATCH` on balances returns `405` or `404` (F5.AC15, F5.R11);
  - all endpoints return `401` with no session.
- **Done when:** the tests pass and no balance is computed or accepted from the client.
- _Needs human review: authorization and ownership checks._

### F5.T5 — Front end: Balances panel and balance tags
- **Files:** `app/src/web/src/app/balances/balances.service.ts`, `balances-panel/balances-panel.component.{ts,html,scss}`, `balance-tag.ts` (tag words and sign rule), `money.ts` (add thousands separators and two decimals if not already from F4), specs; `trips/trip-page/trip-page.component.{ts,html}` (adds the panel beside Expenses).
- **Change:**
  - The panel heading is "Balances" (mockup `F2–F5 · Trip page expenses, people, balances.png`). It shows "Loading balances…" while loading.
  - The list is a real `<ul>` in `seq` order (F5.R4). Each row has the name, the initial in a coloured circle (decorative, `aria-hidden`), the tag, and the bar (decorative, `aria-hidden`).
  - Tag words (F5.R6): "is owed {amount}" for `owed`, "owes {amount}" for `owes`, "settled up" for `settled`, with no minus sign. The amount is formatted from the absolute cents with two decimals and thousands separators (F5.R9, F5.AC11).
  - Bar (F5.R6, F5.AC17): the bar starts at the centre tick. A positive balance extends right in green, a negative balance extends left in red. The width is `min(|balanceCents| / 5000, 1) × 50%` of the track, so 50.00 reaches the end of the half-track, and larger balances stop there. A zero balance shows only the grey centre tick.
  - Footer: "Balances add up to 0.00" with a decorative check mark (mockup, final).
  - Empty state: "Add people to the trip to see balances." when there are no people. A trip with people and no expenses shows every row as "settled up" (States table).
  - "Nobody owes anything yet." appears above the rows when there are no expenses (States table).
  - A `balances_inconsistent` response hides the rows and shows "Balances could not be checked. Please try again." in the panel.
  - A `404` makes the trip page show "This trip no longer exists." once.
  - Each row's text reads in full for screen readers, for example "Sam, is owed 50.00" (Accessibility).
  - The tags carry words, and colour is not the only signal (F5.R6).
- **Covers:** F5.R4, F5.R5, F5.R6, F5.R9, F5.R11 (no input), F5.AC1, F5.AC2, F5.AC3, F5.AC4, F5.AC6, F5.AC8, F5.AC11, F5.AC15, F5.AC16 (panel side), F5.AC17.
- **Tests:** front end (Angular Testing Library, mocked HTTP):
  - the loading text shows first;
  - Example 1 shows "is owed 50.00" for Sam, "owes 10.00" for Alex and "owes 40.00" for Jo, with the footer "Balances add up to 0.00" (F5.AC1, F5.AC2);
  - a zero balance shows "settled up" and no amount (F5.AC3);
  - a green tag reads "is owed 50.00" and a red tag reads "owes 10.00" (F5.AC4);
  - rows render in the order returned (F5.AC6);
  - `1842.50` shows as `1,842.50` (F5.AC11);
  - the bar for 5000 reaches the halfway point of the track, and 8000 stops at the end of the track while its tag still reads "is owed 80.00" (F5.AC17);
  - the bar is `aria-hidden`, and the row's accessible text contains the name and tag;
  - the inconsistent response hides the rows and shows the error copy (F5.AC16);
  - the panel has no input control (F5.AC15);
  - the list is a `list` with the count announced.
- **Done when:** the tests pass and the panel matches the mockup's tag colours, bar and footer.

### F5.T6 — Front end: summary tiles
- **Files:** `app/src/web/src/app/balances/summary-tiles/summary-tiles.component.{ts,html,scss}`, spec; `trips/trip-page/trip-page.component.{ts,html}` (adds the tiles above the panels).
- **Change:** two tiles in this order: **Total spent** (`totalSpentCents`, F4.R13) and **Left to settle** (`leftToSettleCents`). Labels: "Total spent" and "Left to settle" (mockup, final). The tiles are headings with the numbers in text, so a screen reader reads "Total spent, 120.00". The "Biggest payer" tile is not built (F5 spec, Out). The "in {n} payments" text under Left to settle is added in F6, so F5 shows the amount only. Loading and error states follow the panel.
- **Covers:** F5.R8, F5.AC9, F5.AC10.
- **Tests:** front end: Example 1 shows "120.00" under Total spent and "50.00" under Left to settle (F5.AC9, F5.AC10); the tiles appear in that order; the tiles are headings; a `404` shows the trip-gone message once; no "Biggest payer" tile.
- **Done when:** the tests pass.

### F5.T7 — Trip page layout and trip list check (touches F2 and F4 code)
- **Files:** `trips/trip-page/trip-page.component.{ts,html,scss}` (the layout only); `trips/trip-list/` (test only); F2 and F4 trip-page specs get new HTTP stubs only.
- **Change:**
  - The trip page puts the tiles above, and the Expenses and Balances panels side by side, with People below the Balances panel on narrow screens. No existing F2, F3 or F4 control moves out of reach or changes its label.
  - The trip list is checked, not changed: cards show no balance status (F5.AC13).
- **Covers:** F5.AC13, F5.R10; layout for F5.R8 and F5.R5.
- **Tests:** front end: the trip page shows tiles, Expenses and Balances in one layout at desktop width and stacks them at phone width with no horizontal scroll; the trip list card shows no balance text (F5.AC13, F5 only; this test is retired in F6.T12). Existing F2, F3 and F4 trip-page tests pass with only the added stubs.
- **Done when:** the tests pass with no existing assertion changed, and the phone-width check passes.
- _Needs human review: edits existing trip-page tests (project rules)._

### F5.T8 — End-to-end tests
- **Files:** `app/e2e/balances.spec.ts` and shared helpers.
- **Change:** Playwright against the local stack and a non-shared database. Setup creates people and expenses through the API.
- **Covers:** F5.AC1, F5.AC2, F5.AC5, F5.AC7, F5.AC8, F5.AC9, F5.AC10, F5.AC11, F5.AC12, F5.AC13, F5.AC15.
- **Tests:**
  - Example 1 in the UI: tags, footer, tiles (F5.AC1, F5.AC2, F5.AC9, F5.AC10);
  - Example 2 in the UI: Sam is owed 6.66, Alex and Jo owe 3.33 each (F5.AC5);
  - adding and deleting an expense updates the panel and tiles (F5.AC7);
  - a trip with people and no expenses shows "settled up" for every person (F5.AC8);
  - a second user gets "This trip no longer exists." for the first user's trip (F5.AC12);
  - the trip list shows no balance status (F5.AC13);
  - the panel has no control to edit a balance (F5.AC15).
- **Done when:** the suite passes headless with no state shared between tests.

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F5.R1 | T1, T4 | AC1, AC14 | T1 formula with payments zero; T4 Example 1 and AC14 |
| F5.R2 | T1, T5 | AC4 | T1 status by sign; T5 tag words |
| F5.R3 | T1, T3, T4, T5 | AC2, AC16 | T1 zero-sum check and random sets; T3 forced inconsistency; T4 `500`; T5 error copy |
| F5.R4 | T1, T2, T4, T5 | AC3, AC6, AC8 | T1 zero balance listed and `seq` order; T2 order; T5 rows |
| F5.R5 | T5 | AC4 | T5 row layout and accessible text |
| F5.R6 | T1, T5 | AC3, AC4, AC17 | T1 status; T5 tag words, sign, bar scale |
| F5.R7 | T3, T4 | AC7 | T3 and T4 add and delete |
| F5.R8 | T1, T4, T6 | AC9, AC10 | T1 summary; T4 summary endpoint; T6 tiles |
| F5.R9 | T5 | AC11 | T5 `1,842.50` formatting |
| F5.R10 | T7 | AC13 | T7 trip list check |
| F5.R11 | T2, T4, T5 | AC15 | T4 no input; T5 no control |
| F5.R12 | T2, T3, T4 | AC12 | T2 owner join; T3 foreign trip; T4 identical `404` |

| Acceptance criterion | Tasks | Test |
|---|---|---|
| F5.AC1 | T1, T4, T5, T8 | T1 Example 1; T4; T5; T8 |
| F5.AC2 | T1, T4, T5, T8 | T1 sum is `0`; T4; T5 footer; T8 |
| F5.AC3 | T1, T5 | T1 zero balance is `settled`; T5 "settled up" with no amount |
| F5.AC4 | T5 | T5 green and red tags with words |
| F5.AC5 | T1, T4, T8 | T1 Example 2; T4; T8 |
| F5.AC6 | T1, T2, T5 | T1 and T2 `seq` order; T5 rows in order |
| F5.AC7 | T3, T4, T8 | T3 add and delete; T4; T8 |
| F5.AC8 | T1, T4, T8 | T1 and T4 settled and summary `0`; T8 |
| F5.AC9 | T1, T4, T6 | T1 Left to settle `5000`; T4; T6 tile |
| F5.AC10 | T1, T4, T6 | T1 `12000`; T4; T6 tile |
| F5.AC11 | T5 | T5 `1,842.50` |
| F5.AC12 | T2, T3, T4, T8 | T2 foreign read; T3; T4 identical `404`; T8 |
| F5.AC13 | T7, T8 | T7 card check; T8 |
| F5.AC14 | T1, T4 | T1 payer not a participant; T4 |
| F5.AC15 | T4, T5, T8 | T4 no input; T5 no control; T8 |
| F5.AC16 | T1, T3, T4, T5 | T1 check; T3 forced inconsistency; T4 `500` and log; T5 error copy |
| F5.AC17 | T5 | T5 bar scale and clip |

## Test plan
- **Unit (xUnit, Core):** the formula, status, zero-sum check and summary (T1); the service with a fake DAO, including a forced inconsistency and foreign-trip handling (T3).
- **Integration (WebApplicationFactory + Testcontainers PostgreSQL):** the owner-checked read and ordering (T2); both endpoints, the identical `404`, the `500` with no amounts, the no-input rule and the `401`s (T4).
- **Front end (Angular Testing Library):** the panel, tags, bar scale and footer (T5); the summary tiles (T6); the trip page layout and the trip-list check (T7).
- **E2E (Playwright):** the flows in T8.
- **Worked examples:** Example 1 and Example 2 are named tests in T1, T4, T5 and T8. Example 3 belongs to F6.
- **Manual checks:** phone-width layout of the tiles, panels and bars, with no horizontal scroll; screen reader reading each balance row in full; 4.5:1 contrast on tags, including the green and red tags; the bar's meaning is carried by the tag words.

## Risks & mitigations
- **F1–F4 are not built (blocker).** F5 starts only after F4's tasks T2, T4 and T7 exist. If their names change, update F5's tasks.
- **No cache.** Balances are calculated on each request. This is simple and always current (F5.R7). If trips grow large, a cache would need to be cleared on every expense and payment change.
- **Payments term is zero in F5.** The formula includes the payment terms, but `payments` does not exist until F6. F5's tests cover only the zero case. F6 must add the payment tests to this formula (listed in F6).
- **The bar is a layout percentage.** The fixed 50.00 scale is applied with a CSS percentage. The percentage is never summed or stored, so the rule about money as integers still holds.
- **"Nobody owes anything yet" and "settled up" both appear for an empty trip.** The spec states both (States table and F5.AC8). The plan shows "Nobody owes anything yet." above the rows and "settled up" in each row. Confirm this is the intended look when the design is reviewed.
- **Log content for the inconsistency.** The plan logs the trip id and the number of people only, because the spec says the log holds no amounts. This is a proposal in the spec's Edge cases (F5.AC16). If you want the amounts logged for debugging, that is a logging decision and needs your approval.
- **Trip page tests (T7).** The layout change touches F2 and F4 trip-page tests. Only HTTP stubs are added. T7 is marked for human review.
- **Copy the spec does not give:** the phone-width stacking order. The plan stacks Balances below Expenses. Confirm when the design is reviewed.
- **Open questions in the spec:** none.

## Out of scope
- The "Biggest payer" tile (spec, Out).
- Balance status on trip list cards, which waits for F6 (F5.R10).
- Suggested payments, payments, marking them paid and the "in {n} payments" text (F6).
- Editing a balance (product.md, Assumptions).
- Currency, rounding other than the whole-cent rule (product.md, Assumptions).

## Decisions log
- 2026-10-21: _(Proposed in the plan, not asked; no product behavior changes.)_ Balances are calculated on each request and not cached. A cache would need clearing on every expense and payment change, and the calculation is cheap for a trip of this size.
- 2026-10-21: _(Proposed.)_ The formula includes the payment terms now, set to zero until F6 adds the `payments` table, so F6 does not change this formula.
- 2026-10-21: _(Proposed.)_ The shares come from F4's `ExpenseShares`, so the leftover-cent rule is defined once.
- 2026-10-21: _(Proposed.)_ A failed zero-sum check returns `500` `balances_inconsistent` with no amounts, and the log records the trip id and the person count only.
- 2026-10-21: _(Proposed.)_ The bar scale is a CSS percentage from integer cents. It is layout only.
- 2026-10-21: _(Proposed.)_ On narrow screens, the Balances panel stacks below the Expenses panel.
