# F5 — Balances

_Status: Approved · Last updated: 2026-10-20 · Source: product.md, MVP scope (F5 row)_

## Summary
Each person on a trip has a balance: how much they are owed, or how much they owe. A balance is worked out from what they paid, their share of every expense, and any payments they have made or received. The balances always add up to exactly zero, so the app never shows money that appears from nowhere. Balances are what F6 uses to suggest the fewest payments to settle up.

## User stories
- As a user, I want to see what each person is owed or owes, so that I know who should pay whom.
- As a user, I want the balances to add up to zero, so that I can trust them.
- As a user, I want the balances to update as I add and delete expenses, so that they are always current.

## Scope
**In:**
- Each person's balance, shown as "is owed", "owes", or "settled up" (F5.R1–R4)
- The check that all balances add up to 0.00 (F5.R3)
- Balances panel on the trip page, with a bar and tag per person (F5.R5–R6)
- Trip page summary tiles: Total spent and Left to settle (F5.R8)
- Balances update when expenses change (F5.R7)

**Out:**
- Suggested payments, marking them paid, and deleting payments. F6 (product.md, F6 row). This spec only defines the payments term in the balance formula (F5.R1).
- The "Biggest payer" tile. Not in product.md, and not chosen for MVP.
- Balance status on trip list cards. The user chose to wait for F6 (F5.R10).
- Editing a balance. Balances are worked out from expenses and payments, never typed in (product.md, Assumptions: "No editing.").
- Currency, rounding other than the whole-cent rule, or any display of cents beyond two decimal places (product.md, Assumptions).

## User flow & screens
1. The user opens a trip (F2.R6). The trip page shows the summary tiles at the top and the **Balances** panel on the right, beside the Expenses panel (F4).
2. The **Balances** panel lists every person on the trip, in the order they were added (F5.R4).
3. Each row shows the person's name, their initial, a tag with the balance, and a bar (F5.R5).
4. The panel's footer shows **Balances add up to 0.00** (F5.R3).
5. When an expense is added or deleted, the panel and the tiles update (F5.R7).
6. A trip with no people shows the empty state in the panel (States table).

Mockups (in `docs/design/mockups/`):
- **Trip detail** (`Trip detail expenses, people, balances.png`): the three summary tiles (Total spent, Left to settle, Biggest payer, of which only the first two are in F5), and the Balances panel. The tag colours, bar, footer and labels are taken from this mockup.
- **Settle up** (`Settle up.png`): the same Balances panel appears here, beside the suggested payments. The panel is built once for F5 and reused by F6.

Screen names: trip page summary tiles, Balances panel.

## Requirements
- **F5.R1** A person's balance = what they paid − their share of all expenses + payments they made − payments they received. _(product.md, R4.)_ In F5, the payments terms are zero, because payments are created in F6. The formula includes them so F6 needs no change to F5.
- **F5.R2** A positive balance means the person is owed that amount. A negative balance means they owe that amount. A balance of zero means they are settled up. _(product.md, Key terms.)_
- **F5.R3** All balances on a trip add up to exactly 0.00 (product.md, R5). The footer of the Balances panel shows the total as "Balances add up to 0.00".
- **F5.R4** The Balances panel lists every person on the trip, in the order they were added (the same order as the People panel, F3.R5). A person with a zero balance is still listed.
- **F5.R5** Each balance row shows the person's name, their initial in a coloured circle, a tag, and a bar (mockup).
- **F5.R6** The tag reads "is owed {amount}" for a positive balance, "owes {amount}" for a negative balance, and "settled up" for zero. The amount is shown without a minus sign. The tag's words carry the meaning, not its colour alone. The bar runs from a centre line on the track. A positive balance extends right, in green, and a negative balance extends left, in red. The scale is fixed: a balance of 50.00 reaches halfway from the centre to the end of the track, and smaller balances are proportional to it. A balance larger than 50.00 stops at the end of the track, and its tag still shows the full amount. A zero balance shows only the centre tick, in grey. _(Scale from the mockup, decided by the user.)_
- **F5.R7** Balances update when an expense is added or deleted (F4.R1, F4.R9), and when payments are made or deleted (F6).
- **F5.R8** The trip page shows two summary tiles, in this order: **Total spent** (the total of all expenses, F4.R13) and **Left to settle**. Left to settle is the sum of all positive balances, the amount still owed across suggested payments. The payment count in the mockup ("in 2 payments") is added with F6.
- **F5.R9** Money is shown with two decimal places always, and with a thousands separator, as in the mockup (for example, `1,842.50`).
- **F5.R10** Trip list cards show no balance status in F5 (F2.R5). Settled-up and payments-to-settle status are added with F6. _(Decided by the user.)_
- **F5.R11** Balances are calculated from the trip's expenses and payments. Nobody can type or change a balance. _(product.md, Assumptions: "No editing.")_
- **F5.R12** Every balance request is checked against the logged-in user and the trip owner (F1.R9). A request for a trip owned by someone else, or for a trip that does not exist, gets the same "not found" response.

## Data & contract
**None stored.** Balances are calculated when needed from:
- expenses, with each participant's share (F4)
- payments (F6; empty until F6 is built)

The plan decides whether to cache the results. Any cache must be updated on every expense or payment change (F5.R7).

**Browser storage:** None.

**API (paths and exact shapes are decided in the plan):**
- **Get balances:** returns each person in added order, with their balance and status ("owed", "owes" or "settled"), and the total of all balances. The total is always 0.00 when the data is correct (F5.R3).
- **Get summary:** returns Total spent and Left to settle, both as whole cents.

Both need a logged-in session and an owned trip (F5.R12).

## States & errors
Copy below is a **draft** from Claude, for your review. It becomes final once the spec is approved. Copy marked "(mockup)" comes from the design and is final.

| State | When | Behavior | Copy |
|---|---|---|---|
| Loading | The panel or tiles are loading | Loading indicator in the panel and tiles | "Loading balances…" |
| No people | Trip has no people (F3) | Empty state in the panel | "Add people to the trip to see balances." |
| No expenses | Trip has people but no expenses | Every person shows "settled up". Left to settle is 0.00 | "Nobody owes anything yet." |
| Balance check failed | The total of balances is not 0.00 (F5.R3). This should not happen | The balances are not shown. Error in the panel | "Balances could not be checked. Please try again." |
| Network or server error | Request fails | Shown in the panel | "Something went wrong. Please try again." (F1 copy) |
| Trip not found | Request for a trip the user does not own, or a missing trip (F5.R12) | Same as F2 | "This trip no longer exists." (F2 copy) |

Tag wording (mockup, final): "is owed {amount}", "owes {amount}", "settled up". Footer (mockup, final): "Balances add up to 0.00". Tile labels (mockup, final): "Total spent", "Left to settle".

## Accessibility
Target: **WCAG 2.2 AA**, the project target set in F1.
- The Balances list is a real list. Screen readers announce the number of people.
- Each row's text reads in full, for example "Sam, is owed 50.00". The bar is decorative (`aria-hidden`), because the tag already carries the same information.
- The tags use words, not colour alone (F5.R6). Green and red tags are also readable without colour.
- The footer's check mark is decorative. The words carry the meaning.
- Text and tag colours meet at least 4.5:1 contrast (WCAG 1.4.3).
- The summary tiles are headings with their numbers in text, so screen readers read "Total spent, 120.00".
- No animation is needed. Reduced-motion support applies only if animation is added later.

## Edge cases
- A balance of exactly 0.00 shows "settled up", never "owes 0.00" (F5.R6).
- A payer who is not a participant has a positive balance equal to what they paid (F4.R6).
- A person who is in no expense shows "settled up" (F5.R6). They are still listed (F5.R4).
- Example 1 (product.md worked example): Sam paid 90.00 and Alex paid 30.00, both shared by Sam, Alex and Jo. Sam is owed 50.00, Alex owes 10.00, Jo owes 40.00. The balances total 0.00.
- Example 2 (product.md worked example): Sam paid 10.00, shared by Sam, Alex and Jo, added in that order. Sam's share is 3.34, Alex's 3.33, Jo's 3.33. Sam is owed 6.66, Alex owes 3.33, Jo owes 3.33. The balances total 0.00.
- Deleting the only expense on a trip returns every person to "settled up" (F5.R7).
- A balance above 999.99 is shown with a thousands separator (F5.R9).
- If the balance total is not 0.00, the balances are not shown. The app logs the problem without any amounts, and shows the error copy. This is a proposal, not stated in product.md, and is tested with a forced inconsistency (F5.AC16).
- Left to settle is the sum of positive balances. It goes down as payments are made (F6, because payments change balances under F5.R1), and it is 0.00 once every balance is zero.

## Acceptance criteria
- **F5.AC1** Given Example 1 (Sam paid 90.00, Alex paid 30.00, all three share both), when I view balances, then Sam shows "is owed 50.00", Alex shows "owes 10.00" and Jo shows "owes 40.00".
- **F5.AC2** Given Example 1, when I view the Balances panel, then the footer shows "Balances add up to 0.00".
- **F5.AC3** Given a person with a balance of zero, when I view the panel, then their tag reads "settled up" and shows no amount.
- **F5.AC4** Given a person is owed 50.00, when I view their tag, then it reads "is owed 50.00" in the green style, and a person who owes 10.00 reads "owes 10.00" in the red style. The words are present in each case.
- **F5.AC5** Given Example 2 (Sam paid 10.00, shared by Sam, Alex and Jo in that order), when I view balances, then Sam is owed 6.66, Alex owes 3.33 and Jo owes 3.33, and the total is 0.00.
- **F5.AC6** Given a trip where I added Sam, then Alex, then Jo, when I view the Balances panel, then the rows are in the order Sam, Alex, Jo.
- **F5.AC7** Given the Balances panel is showing, when I add an expense, then the balances change. When I delete that expense, then they return to their earlier values.
- **F5.AC8** Given a trip with people and no expenses, when I view the panel, then every person shows "settled up", and Left to settle shows 0.00.
- **F5.AC9** Given Example 1, when I view the tiles, then Left to settle shows 50.00 (the sum of the amounts owed, Alex 10.00 and Jo 40.00).
- **F5.AC10** Given Example 1, when I view the tiles, then Total spent shows 120.00.
- **F5.AC11** Given a balance of 1842.50, when I view it, then the amount shows as `1,842.50`.
- **F5.AC12** Given user B requests user A's trip balances, then B gets "This trip no longer exists." (F5.R12).
- **F5.AC13** Given I am on a trip list, when I view a card, then it shows no balance status (F5.R10).
- **F5.AC14** Given Sam paid 30.00 for an expense shared by Alex and Jo, with Sam not a participant, when I view balances, then Sam is owed 30.00, Alex owes 15.00 and Jo owes 15.00.
- **F5.AC15** Given the Balances panel, when I look for a control, then none lets me type or change a balance (F5.R11).
- **F5.AC16** Given the balances do not add up to 0.00 (forced in a test), when I view the panel, then the balances are not shown and the error copy appears.
- **F5.AC17** Given a balance of 50.00, when I view its bar, then the bar reaches halfway from the centre to the end of the track. Given a balance of 80.00, then the bar stops at the end of the track and the tag reads "is owed 80.00".

## Dependencies
- **Depends on:** F1 (login and ownership, F1.R9), F2 (trips and trip page), F3 (people and their order, F3.R5), F4 (expenses, shares and total, F4.R13).
- **Needed by:** F6 (suggested payments are based on balances, and payments change balances, F5.R1 and F5.R7). F6 also reuses the Balances panel.
- **Build order (§8):** not in product.md, so the order given by the MVP table is used. F5 is the fifth step.
- **External services:** none.

## Test plan
- **Unit:** balance formula: paid − share + payments made − payments received (F5.AC1, F5.AC14, F5.R1).
- **Unit:** the sum of all balances is 0.00 for many random expense sets (F5.AC2, F5.R3).
- **Unit:** the tag rules: "is owed", "owes", "settled up" for positive, negative and zero, with no minus sign (F5.AC3, F5.AC4, F5.R6).
- **Unit:** bar length is proportional to the balance on the fixed 50.00 scale, and clips above it (F5.AC17, F5.R6).
- **Unit:** Example 2 rounding, with shares and balances computed to the cent (F5.AC5).
- **Unit:** order of the balance list by added time (F5.AC6, F5.R4).
- **Unit:** Left to settle equals the sum of positive balances (F5.AC8, F5.AC9, F5.R8).
- **Unit:** formatting: two decimal places and thousands separators (F5.AC11, F5.R9).
- **Unit:** the balance check refuses to show totals that are not 0.00 (F5.AC16).
- **Integration:** adding and deleting an expense updates balances and tiles (F5.AC7).
- **Integration:** a trip with no expenses shows all people settled up (F5.AC8).
- **Integration:** user B cannot read user A's balances (F5.AC12, F5.R12).
- **Integration:** the trip list shows no balance status (F5.AC13).
- **End-to-end:** the Balances panel has no control to edit a balance (F5.AC15).

## Open questions
- [x] Bar scale (F5.R6): decided on 2026-10-20. Fixed scale from the mockup, with a 50.00 balance at halfway and larger balances clipped at the track end.
- [x] Copy: approved with the spec on 2026-10-20.

## Decisions log
- 2026-10-20: How should the Balances list be sorted? → Order added to the trip.
- 2026-10-20: Should each balance show a bar, as in the mockup, or only the tag and amount? → Bar and tag, as in the mockup.
- 2026-10-20: Which summary tiles should the trip page show in F5? → Total spent and Left to settle. Biggest payer is not included.
- 2026-10-20: Should the trip list cards show balance status now, or wait until F6? → Wait for F6.
- 2026-10-20: Bar scale? → Follow the mockup: fixed scale, 50.00 at halfway. Balances above 50.00 stop at the track end (tag shows the full amount).
- 2026-10-20: Approve the spec? → Yes. Status set to Approved. Copy approved.
