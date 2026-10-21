# F6 — Settle up

_Status: Approved · Last updated: 2026-10-20 · Source: product.md, MVP scope (F6 row)_

## Summary
After the expenses are in, the app proposes the fewest payments it can to bring every balance to zero, and the user marks each one as paid once the money has moved outside the app. The app only records that a payment was made. Marking a payment as paid changes the balances, so the proposals update until everyone is settled. A recorded payment can be deleted if it was marked by mistake.

## User stories
- As a user, I want to see who should pay whom and how much, so that the trip ends with everyone settled.
- As a user, I want to mark a payment as paid once it has been made, so that the balances reflect it.
- As a user, I want to delete a payment I marked by mistake, so that the balances are correct again.

## Scope
**In:**
- Suggested payments, worked out by the rule in F6.R2–R4 (F6.R1)
- Mark a suggested payment as paid, recorded in full (F6.R5–R6)
- A list of payments made, newest first, with a delete control (F6.R7)
- Delete a payment after confirmation (F6.R8)
- Settle up view: summary tiles and the Balances panel (F6.R12–R13)
- Settled state when every balance is zero (F6.R14)

**Out:**
- Partial payments. A payment is a suggested payment marked as paid, so it is always the full suggested amount (product.md, Key terms).
- Recording a payment that was not suggested. Same reason.
- Editing a payment. product.md, Scope: "can be added and deleted, not edited."
- Moving money or any payment provider. product.md, Assumptions: "Money moves outside the app."
- Currency and conversion. product.md, Assumptions.
- Notifications or reminders to people who owe money. Not in product.md.
- Checking a person's account or paying them on the app. Persons have no accounts (product.md, Roles).

## User flow & screens
1. The user opens a trip (F2.R6) and selects **Settle up** in the trip's view switch. The Expenses view is the other option (mockup).
2. The summary tiles show **Paid back so far**, **Left to settle** and **Total spent** (F6.R12).
3. The **Suggested payments** panel lists each payment in order: payer, payee, amount, and a **Mark as paid** button (F6.R1).
4. **Mark as paid:** the user selects **Mark as paid** on a suggestion. A confirmation dialog appears.
   - **Cancel** closes the dialog and records nothing.
   - **Mark as paid** records the payment in full and returns to the view. The suggestions and balances update (F6.R5, F6.R11).
5. **Payments made:** every recorded payment is listed, newest first, with its date and a delete control (F6.R7).
6. **Delete a payment:** the user selects the trash icon. A confirmation dialog appears.
   - **Cancel** keeps the payment.
   - **Delete payment** removes it. The balances and suggestions revert (F6.R8).
7. **Settled:** when every balance is zero, the Suggested payments panel shows that everyone is settled up (F6.R14).

Mockups (in `docs/design/mockups/`):
- **Settle up** (`Settle up.png`): the summary tiles (Paid back so far, Left to settle, Total spent), the Suggested payments panel with its helper text and the **Mark as paid** button, the Payments made list, and the Balances panel. The labels, the helper text, the progress bar and the row layout are taken from this mockup.
- **Delete trip confirmation** (`Delete trip confirmation.png`): the dialog pattern. The payment delete dialog uses the same layout, with the copy in the States table.
- **Trip detail** (`Trip detail expenses, people, balances.png`): the view switch shows Expenses and Settle up.

Screen names: Settle up view, suggested payments panel, mark-as-paid dialog, payments made list, delete payment dialog.

## Requirements
- **F6.R1** The app shows the suggested payments that bring every balance to zero. Each suggestion is a payer, a payee and an amount, all in whole cents. _(product.md, R6.)_
- **F6.R2** Suggested payments are worked out from the current balances (F5.R1). The app repeats this rule until every balance is zero. The largest debtor (the person who owes the most) pays the largest creditor (the person owed the most). The amount is the smaller of the two balances. _(product.md, R6. Example 1: Jo pays Sam 40.00, then Alex pays Sam 10.00.)_
- **F6.R3** When two people are tied for the largest debt, or for the largest credit, the one added to the trip first comes first (F3.R5). _(Decided by the user.)_
- **F6.R4** The method in F6.R2 is the rule of product.md, R6. It may sometimes suggest one more payment than the fewest possible. product.md, Goals, asks for "as few payments as possible". This spec follows R6. _(Decided by the user: follow R6 as written.)_
- **F6.R5** A user can mark a suggested payment as paid. The app then asks for confirmation (F6.R6 and the States table). Once confirmed, the payment is recorded.
- **F6.R6** A recorded payment has a payer, a payee and an amount, and it is recorded in full. It is not partial. The payer and payee are different people on the same trip. The payment records the time it was marked paid (mockup: "Marked paid · 20 Oct").
- **F6.R7** The payments made list shows every recorded payment for the trip, newest first (F4.R8). Each row shows "{payer} paid {payee}", the amount, the date it was marked paid, and a delete control. _(Decided by the user.)_
- **F6.R8** Deleting a payment asks for confirmation in a dialog with **Delete payment** and **Cancel**. **Cancel** keeps the payment. **Delete payment** removes it. The balances and the suggested payments update as if the payment had never been marked.
- **F6.R9** Payments cannot be edited (product.md, Scope). A mistake is fixed by deleting the payment and marking the right one as paid.
- **F6.R10** A payment changes the balances. The payer's balance goes up by the amount paid, and the payee's balance goes down by the same amount (product.md, R4; F5.R1). This keeps the total at 0.00 (F5.R3).
- **F6.R11** After every change to payments, the suggestions are recalculated from the new balances. The number of suggestions can go down, as in product.md Example 3: after Jo pays Sam 40.00, one suggestion is left, Alex pays Sam 10.00.
- **F6.R12** The Settle up view shows three tiles, in this order:
  - **Paid back so far**: the total of all recorded payments, with "of {owed} owed" beneath it. The owed total is the paid-back total plus Left to settle, so it stays the same as payments are made or deleted.
  - A progress bar and the percentage of the owed total that has been paid back, rounded down to a whole percent. The percentage reads 100% only when Left to settle is 0.00.
  - **Left to settle**: the amount from F5.R8, with the number of suggested payments beneath it ("in 1 payment", "in 2 payments", "in 0 payments").
  - **Total spent**: the total from F4.R13.
  _(Mockup labels and layout, final. The rounding rule and the "of owed" meaning are decided by the user, see Decisions log.)_
- **F6.R13** The Settle up view shows the Balances panel from F5, unchanged. _(Mockup. The panel is built once for F5 and reused here.)_
- **F6.R14** When every balance is zero, the Suggested payments panel shows that everyone is settled up, and Left to settle shows 0.00 in 0 payments.
- **F6.R15** Every settle-up request (list, mark as paid, delete payment) is checked against the logged-in user and the trip owner (F1.R9). A request for a trip owned by someone else, or for a trip that does not exist, gets the same "not found" response.
- **F6.R16** A person who is in a recorded payment cannot be removed from the trip (F3.R7). Their People row shows "In payments" (F3.R7, decided for now).
- **F6.R17** Deleting a trip deletes its payments (F2.R12).
- **F6.R18** A payment is recorded only for the suggestion the user saw. If the suggestions have changed, the payment is not recorded, and the user is asked to review them again (F6.AC9). _(Decided by the user.)_

## Data & contract
**Payment (new record):**
- `id`
- `trip_id`: the trip. Required. Indexed. (F2)
- `payer_person_id`: who paid. Required. Must belong to the same trip.
- `payee_person_id`: who was paid. Required. Must belong to the same trip. Different from the payer.
- `amount_cents`: whole number of cents, greater than 0 (F6.R6)
- `marked_paid_at`: the time the payment was marked paid. Used for ordering (F6.R7).

**Suggested payments:** not stored. They are worked out from the current balances (F6.R2), and the plan decides whether to cache them. Any cache must be updated on every payment change (F6.R11).

**Links from F3:** payer and payee both refer to a person. Each reference blocks that person's removal (F3.R7). This is the reference F3 was waiting for.

**Browser storage:** None.

**API (paths and exact shapes are decided in the plan):**
- **Get settle-up:** returns the suggested payments in order, the payments made (newest first), Paid back so far, the owed total, Left to settle, the number of suggested payments, and the percentage.
- **Mark as paid:** takes the payer, the payee and the amount the user saw. The server checks that this matches a current suggestion, and then records the payment. If it does not match, the request is refused with the error in the States table (F6.R18).
- **Delete payment:** deletes one payment on a trip the user owns.

Errors use the copy in the States table. All requests need a logged-in session and an owned trip (F6.R15).

## States & errors
Copy below is a **draft** from Claude, for your review. It becomes final once the spec is approved. Copy marked "(mockup)" comes from the design and is final.

| State | When | Behavior | Copy |
|---|---|---|---|
| Loading | The Settle up view is loading | Loading indicator in the panels and tiles | "Loading settle up…" |
| Suggestions empty | Every balance is zero, or the trip has no expenses (F6.R14) | Panel shows the settled state | "Everyone is settled up." |
| No payments made | No payments recorded yet | Empty state in the Payments made list | "No payments yet. Payments you mark as paid will appear here." |
| Helper text | Always shown in the Suggested payments panel | Text under the panel heading | "The fewest payments that bring every balance to zero." (mockup) and "Money moves outside the app. Mark a payment as paid once it's been made." (mockup) |
| Nothing to settle | Owed total is 0.00, so the percentage has no meaning | Progress bar hidden. Percentage replaced by text | "Nothing to settle yet." |
| Mark as paid: confirm | User selects Mark as paid (F6.R5) | Dialog with Mark as paid and Cancel | Title: "Mark this payment as paid?" Body: "{payer} pays {payee} {amount}. This records that the money has moved outside the app." Buttons: "Mark as paid", "Cancel" |
| Payment recorded | Success | The suggestions, tiles and balances update | "Payment recorded." |
| Suggestions changed | The server finds the suggestion no longer matches (F6.R18) | Dialog or message in the panel. Nothing recorded. The suggestions reload | "The suggested payments have changed. Review them and try again." |
| Delete payment: confirm | User selects the trash icon (F6.R8) | Dialog with Delete payment and Cancel | Title: "Delete this payment?" Body: "{payer} paid {payee} {amount}. Deleting it puts the amount back into the balances." Buttons: "Delete payment", "Cancel" |
| Payment deleted | Success | The row disappears. Suggestions, tiles and balances update | "Payment deleted." |
| Payment not found | Deleted in another tab, or not on this trip (F6.R15) | Message in the Payments made list | "This payment no longer exists." |
| Trip not found | Request for a trip the user does not own, or a missing trip (F6.R15) | Same as F2 | "This trip no longer exists." (F2 copy) |
| Network or server error | Request fails | Shown above the panel or in the dialog | "Something went wrong. Please try again." (F1 copy) |

Tile labels (mockup, final): "Paid back so far", "of {owed} owed", "Left to settle", "in {n} payments", "Total spent", "across {n} expenses". Row layout (mockup, final): "{payer} pays {payee}", "Settles {payer}'s balance". Button: "Mark as paid". Payments made heading (mockup, final): "Payments made", "{n} payments". Payment row (mockup, final): "{payer} paid {payee}", "Marked paid · {date}".

Plural forms: "1 payment" and "1 expense" for one; "0 payments" and "N payments" for others.

## Accessibility
Target: **WCAG 2.2 AA**, the project target set in F1.
- The Expenses and Settle up views are a two-option switch. Each option is a button with its state announced (for example `aria-pressed`), and the switch is labelled "Trip view".
- The progress bar is a `progressbar` with a label ("Paid back so far"), its value, and the percentage in text. The percentage carries the meaning, not the bar.
- Each suggested payment's **Mark as paid** button has an accessible name with the names and amount, for example "Mark Alex pays Sam 10.00 as paid", so a screen reader user can tell the rows apart.
- Each payment's delete control has an accessible name with the payment, for example "Delete Jo paid Sam 40.00".
- The confirmation dialogs trap focus while open. Escape closes them and returns focus to the button that opened them.
- Amounts and names are text. The payment direction ("pays" and "paid") is in the words, not in arrows alone. The dashed arrow in the mockup is decorative (`aria-hidden`).
- Text and status colours meet at least 4.5:1 contrast (WCAG 1.4.3).
- Controls are at least 24×24 CSS pixels (WCAG 2.5.8).
- No animation is needed. Reduced-motion support applies only if animation is added later.

## Edge cases
- Every balance is zero: no suggestions. Left to settle shows 0.00 in 0 payments, and the settled state is shown (F6.R14).
- Marking a suggestion as paid moves the payer's and payee's balances towards zero. The suggestions are then worked out again from the new balances (F6.R11, product.md Example 3).
- A payment is marked paid in full, even if the user thinks someone paid less. Partial payments are not supported (product.md, Key terms).
- Deleting a payment restores both balances, and the suggestions revert to what they were (F6.R8).
- Deleting an expense after payments are recorded can leave balances that differ from the payments. The suggestions follow the new balances (F5.R1), and the total still adds up to 0.00 (F5.R3).
- A payment's amount is fixed when it is marked paid. Later changes to expenses do not change it.
- A person in a recorded payment cannot be removed (F6.R16, F3.R7).
- With two people tied for the largest credit, the earlier-added person is paid first (F6.R3). Example: Jo owes 20.00, and Sam and Alex are each owed 10.00, with Sam added first. Jo pays Sam 10.00, then Alex 10.00.
- Paid back so far is 199.00 of 200.00 owed: 99.5% rounds down to 99%, not 100% (F6.R12).
- Payment dates use the user's local time zone. Every payment shows the full date, day, month and year, for example "20 Oct 2026". The mockup's "20 Oct" is extended with the year. _(Full date decided by the user.)_
- Two payments with the same payer, payee and amount are both kept. Nothing in product.md prevents it.
- A "Mark as paid" on a suggestion that changed in another tab is refused with the copy in the States table (F6.R18).

## Acceptance criteria
- **F6.AC1** Given Example 1 (Sam owed 50.00, Alex owes 10.00, Jo owes 40.00), when I open Settle up, then the suggested payments are Jo pays Sam 40.00 and Alex pays Sam 10.00, in that order.
- **F6.AC2** Given the suggestions of F6.AC1, when I mark "Jo pays Sam 40.00" as paid and confirm, then one suggestion is left: Alex pays Sam 10.00 (product.md, Example 3).
- **F6.AC3** Given all suggestions are marked paid, when I view Settle up, then every balance is zero, the settled state is shown, and Left to settle shows 0.00 in 0 payments.
- **F6.AC4** Given I select Mark as paid, when I choose Cancel, then nothing is recorded and the suggestion stays.
- **F6.AC5** Given I confirm a payment, when I view the Payments made list, then the payment shows "Jo paid Sam", the amount 40.00, and "Marked paid" with today's date.
- **F6.AC6** Given a payment in the list, when I select its delete control and choose Cancel, then the payment stays. When I choose Delete payment, then it is removed, and the balances and suggestions return to what they were before it was marked paid.
- **F6.AC7** Given two people are owed 10.00 each (Sam added first, then Alex) and Jo owes 20.00, when I view the suggestions, then Jo pays Sam 10.00 and then Jo pays Alex 10.00.
- **F6.AC8** Given Example 1 with Jo's payment of 40.00 recorded, when I view the tiles, then Paid back so far shows 40.00 of 50.00 owed, the progress bar shows 80% settled, and Left to settle shows 10.00 in 1 payment.
- **F6.AC9** Given the suggestions have changed in another tab, when I mark a suggestion that no longer matches as paid, then nothing is recorded and I see "The suggested payments have changed. Review them and try again."
- **F6.AC10** Given paid back is 199.00 and owed is 200.00, when I view the percentage, then it shows 99%, not 100%.
- **F6.AC11** Given I have recorded a payment, when I look at the Payments made row, then there is no control to edit it (F6.R9).
- **F6.AC12** Given a person is in a recorded payment, when I try to remove them from the People panel, then the removal is refused (F3.AC8, F6.R16).
- **F6.AC13** Given user B requests user A's settle-up data, then B gets "This trip no longer exists." (F6.R15).
- **F6.AC14** Given a trip with no expenses, when I open Settle up, then the suggestions panel shows "Everyone is settled up.", and the percentage area shows "Nothing to settle yet." (F6.R14, the States table).
- **F6.AC15** Given the Payments made list has payments, when I view it, then the newest payment is at the top (F6.R7).
- **F6.AC16** Given I delete a trip with recorded payments, then those payments are gone (F6.R17).

## Dependencies
- **Depends on:** F1 (login and ownership, F1.R9), F2 (trips, F2.R12 deletion), F3 (people, the in-use check, F3.R7), F4 (expenses and total, F4.R13), F5 (balances, the Balances panel, F5.R1, F5.R3, F5.R8).
- **Needed by:** none in the MVP. F6 is the last feature in the MVP table.
- **Build order (§8):** not in product.md, so the order given by the MVP table is used. F6 is the sixth and last step.
- **External services:** none. Money moves outside the app (product.md, Assumptions).

## Test plan
- **Unit:** the suggestion method (F6.R2): Example 1 gives Jo pays Sam 40.00 and Alex pays Sam 10.00 (F6.AC1).
- **Unit:** tie-breaking by added order (F6.R3, F6.AC7).
- **Unit:** suggestions are recomputed after a payment, and the total stays 0.00 (F6.R10, F6.R11, F6.AC2, F5.AC2).
- **Unit:** the percentage rounds down, and is 100% only when Left to settle is 0.00 (F6.R12, F6.AC10).
- **Unit:** the owed total equals paid back plus Left to settle (F6.R12, F6.AC8).
- **Unit:** payment validation: positive whole cents, payer and payee different, both on the same trip (F6.R6).
- **Unit:** the stale-suggestion check refuses a payment that no longer matches (F6.R18, F6.AC9).
- **Integration:** marking a payment as paid, then deleting it, returns balances and suggestions to the earlier values (F6.AC6).
- **Integration:** marking all suggestions as paid shows the settled state (F6.AC3, F6.AC14).
- **Integration:** cancel on mark-as-paid and delete-payment records and removes nothing (F6.AC4, F6.AC6).
- **Integration:** payments make a person's row "In payments" and block their removal (F6.AC12, F3.R7).
- **Integration:** another user's settle-up data cannot be read or changed (F6.AC13, F6.R15).
- **Integration:** deleting a trip deletes its payments (F6.AC16, F6.R17).
- **End-to-end:** the Payments made rows have no edit control (F6.AC11), and the list is newest first (F6.AC15).

## Open questions
- [x] Copy: the States table is approved on 2026-10-20.
- [x] Stale suggestions (F6.R18): decided on 2026-10-20. The payment is refused and the user is asked to review again.
- [x] Payment dates: decided on 2026-10-20. Every payment shows the full date.

## Decisions log
- 2026-10-20: Settling up method: should F6 follow R6 as written, or search for the true minimum number of payments? → Follow R6 as written. Note that Goals say "as few payments as possible", and R6 may occasionally give one more.
- 2026-10-20: When two people are tied, who comes first? → Earlier added to the trip first.
- 2026-10-20: When a user selects Mark as paid, should the app ask for confirmation? → Yes, a confirmation dialog.
- 2026-10-20: When a user deletes a recorded payment, should the app ask for confirmation? → Yes, a confirmation dialog.
- 2026-10-20: How should the percentage be rounded? → Down to a whole percent.
- 2026-10-20: In what order should the Payments made list show payments? → Newest first.
- 2026-10-20: "Of {owed} owed" in the Paid back tile is the paid-back total plus Left to settle. Derived from the mockup (50.00 = 40.00 + 10.00).
- 2026-10-20: Copy (States table) approved as drafted.
- 2026-10-20: Proposed product.md Goals edit (settling up method)? → No. product.md stays as it is. This spec keeps the R6 method (F6.R4).
- 2026-10-20: Approve the spec? → Yes. Status set to Approved. Copy and both decisions above are included.
- 2026-10-20: Stale suggestions: refuse the payment and ask the user to review again? → Yes.
- 2026-10-20: Payment dates: show the year only for earlier years, or the full date always? → Full date on every payment.
