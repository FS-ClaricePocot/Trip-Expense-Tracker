# F4 — Expenses

_Status: Approved · Last updated: 2026-10-21 · Source: product.md, MVP scope (F4 row)_

## Summary
A user records what was spent on a trip: a description, an amount, who paid, and who shares it. Each expense is split equally between the people who share it, to the cent. Expenses are what the balances (F5) and suggested payments (F6) are built from, so a correct split matters for the whole app. An expense can be deleted, but not edited, so a mistake is fixed by deleting it and adding it again.

## User stories
- As a user, I want to add an expense with what it was for, how much, who paid, and who shares it, so that the trip records the spending.
- As a user, I want every expense split exactly to the cent, so that nobody is charged a fraction of a cent.
- As a user, I want to delete an expense I entered by mistake, so that the balances are correct.

## Scope
**In:**
- Add an expense with a description, amount, payer and participants (F4.R1–R6)
- Equal split to the cent, with leftover cents to the earliest-added people (F4.R7)
- Expense list, newest first, with a total (F4.R8, R13)
- Delete an expense after confirmation (F4.R9)
- The expense is what counts as "in an expense" for people (F3.R7). F4 turns on that check.
- Expense count and total on each trip's card in the trip list (F4.R17–R18). F2 deferred this to the feature that adds expenses.

**Out:**
- Editing an expense. product.md, Assumptions: "No editing."
- Uneven splits, custom shares or percentages. product.md, Assumptions: "Equal splits only." Editing and CSV export are listed under "After the core", only if budget remains.
- Categories. Not in the four inputs (product.md, Goals). The row icon is covered in F4.R10.
- A date for the expense. Not in the four inputs. The time added is recorded for ordering (F4.R8).
- Currency or conversion. product.md, Assumptions: "One currency."
- Balances (F5) and suggested payments and settling up (F6). They read expenses but are specified separately.
- Maximum number of expenses per trip. Not limited (see Decisions log).
- The "Add the first expense" button on a trip card with no expenses (mockup). Not built; the card shows "no expenses yet" only (F4.R17). Decided 2026-10-21.

## User flow & screens
1. The user opens a trip (F2.R6) and sees the **Expenses** view, with the total and the list.
2. **Add:** the user fills in the "Add an expense" form and selects **Add expense**.
   - **What was it for?**: the description (F4.R2).
   - **Amount**: the amount (F4.R3).
   - **Paid by**: the payer, chosen from the trip's people. No one is pre-selected (F4.R4).
   - **Split between**: one checkbox per person. All are checked when the form opens (F4.R5).
   - Errors are shown as described in the States table. The expense appears at the top of the list when it is saved.
3. **Delete:** the user selects the trash icon on an expense. A confirmation dialog appears.
   - **Cancel** keeps the expense.
   - **Delete expense** removes it. The total and the balances update.
4. **No people:** if the trip has no people, the form is disabled with a message (States table).

Mockups (in `docs/design/mockups/`):
- **Trip detail** (`Trip detail expenses, people, balances.png`): the Expenses panel with the "Add an expense" form, the list with the total, and the People and Balances panels from F3 and F5. The form labels, the helper text and the "Add expense" button are taken from this mockup.
- **Delete trip confirmation** (`Delete trip confirmation.png`): the confirmation dialog pattern. The expense delete dialog reuses that layout with the copy in the States table.

Mockup differences in this spec:
- The mockup pre-selects "Sam" as the payer. F4.R4 uses no default, as decided.
- The mockup row says "10.00 each". F4.R10 shows "split N ways" instead, because leftover cents make shares differ.
- The mockup's category icons are replaced by the icon rule in F4.R11.

## Requirements
- **F4.R1** A user can add an expense to a trip with four inputs: a description, an amount, a payer and the people who share it.
- **F4.R2** The description is required. Spaces at the start and end are removed before the description is checked and saved. A description that is blank after trimming is refused. The trimmed description is at most 100 characters. Any characters are allowed.
- **F4.R3** The amount is required. It must be greater than 0.00. It is entered as a decimal with at most 2 decimal places. It is stored as a whole number of cents, never as a floating-point number (product.md, Assumptions). Amounts with more than 2 decimal places are refused, because a fraction of a cent cannot be stored. There is no maximum beyond the range of the stored whole-cent value, and an amount too large to store is refused.
- **F4.R4** The payer is required. It must be one of the people on this trip (product.md, R2). No payer is selected by default.
- **F4.R5** The people who share the expense are the participants. All people on the trip are selected by default. The user may uncheck any of them. At least one participant is required (product.md, R2). Each participant must be a person on this trip.
- **F4.R6** The payer does not have to be a participant (product.md, R2). A payer who is not checked as a participant has a share of 0.00.
- **F4.R7** Each participant's share is the amount divided equally between the participants, rounded down to the cent. The leftover cents are given one each to participants in the order they were added to the trip, starting with the earliest. The shares add up to exactly the amount (product.md, R3). _(Worked example 2: 10.00 among Sam, Alex and Jo, in that order, gives 3.34, 3.33 and 3.33.)_
- **F4.R8** The expense list shows the newest expense first, by the time it was added.
- **F4.R9** Deleting an expense asks for confirmation in a dialog with **Delete expense** and **Cancel**. **Cancel** keeps the expense. **Delete expense** deletes it. The deletion is permanent.
- **F4.R10** Each expense row shows the description, the amount, the payer, and the number of people sharing it, as in "Alex paid · split 3 ways". The row does not show a per-person share, because leftover cents can make shares differ.
- **F4.R10a** Selecting an expense row opens a pop-up dialog. The dialog lists each participant's name and share, in the order they were added, and shows the total. **Close** returns the user to the trip page, where they were. _(Decided by the user: a pop-up dialog.)_
- **F4.R11** Each expense row shows one icon. The app picks it from keywords in the description, and the user does not choose it, so the form keeps its four inputs (product.md, Goals). The icon set is:
  - Transport (car): for example taxi, bus, fuel, flight
  - Food and drink (fork and knife): for example dinner, lunch, groceries, snacks
  - Lodging (bed): for example hotel, rental, homestay
  - Activities (ticket): for example tour, entry, tickets
  - Generic money icon: used when no keyword matches, and the default
  The keyword lists for each icon are an open question. _(Icon set and generic fallback decided by the user. The user chose the app-picks option over a picker.)_
  **Interim (2026-10-21):** until the keyword lists are decided, every row shows the generic money icon. The category icons above do not appear yet.
- **F4.R12** An expense cannot be edited. A mistake is fixed by deleting the expense and adding it again. _(product.md, Assumptions: "No editing.")_
- **F4.R13** The Expenses panel shows the number of expenses and their total. The total is the sum of all expense amounts on the trip (mockup).
- **F4.R14** An expense is removed when its trip is deleted (F2.R12). Removing an expense is also the only way a person stops being "in an expense" (F3.R7).
- **F4.R15** Every expense request is checked against the logged-in user and the trip owner (F1.R9). A request for a trip owned by someone else, or for a trip that does not exist, gets the same "not found" response.
- **F4.R16** The expense list has no limit on the number of expenses per trip. _(Decided by the user.)_

- **F4.R17** Each trip card in the trip list shows the number of expenses: "{n} expenses", or "1 expense" for one. A trip with no expenses shows "no expenses yet". _(Wording from the mockup's card text, with the singular decided on 2026-10-21.)_
- **F4.R18** Each trip card with at least one expense shows "Total spent" and the trip's total (F4.R13). A trip with no expenses shows no total line. _(Decided 2026-10-21.)_

## Data & contract
**Expense (new record):**
- `id`
- `trip_id`: the trip. Required. Indexed. (F2)
- `description`: trimmed text, 1–100 characters (F4.R2)
- `amount_cents`: whole number of cents, greater than 0 (F4.R3)
- `payer_person_id`: the person who paid. Required. Must belong to the same trip (F4.R4, product.md R2)
- `created_at`: used for ordering (F4.R8)

**Expense participant (new record, one row per person sharing an expense):**
- `expense_id`
- `person_id`. Must belong to the same trip. Unique together with `expense_id`.

An expense must have at least one participant. The app enforces this when saving, and the plan decides whether the database also enforces it (product.md, R2).

**Shares:** computed from `amount_cents`, the number of participants, and the order the participants were added (F4.R7). The plan decides whether to store them or compute them when needed.

**Links from F3:** the payer and the participants both refer to a person. Each reference blocks that person's removal (F3.R7). The expense tables are the references F3 was waiting for.

**Browser storage:** None.

**API (paths and exact shapes are decided in the plan):**
- **List expenses:** returns the trip's expenses, newest first, each with its payer, participants and shares, and the total.
- **Add expense:** takes a description, an amount as decimal text with at most 2 decimal places, a payer, and the list of participants. The amount is converted to whole cents on the server. It is refused with a validation error if any rule in F4.R2–R5 fails.
- **Delete expense:** deletes one expense on a trip the user owns.

- **Trip list (F2):** each trip in the list also returns its expense count and total (F4.R17, F4.R18).

All requests need a logged-in session and an owned trip (F4.R15).

## States & errors
Copy below is a **draft** from Claude, for your review. It becomes final once the spec is approved. Copy marked "(mockup)" comes from the design and is final.

| State | When | Behavior | Copy |
|---|---|---|---|
| Loading list | The Expenses panel is loading | Loading indicator in the panel | "Loading expenses…" |
| Empty list | No expenses on the trip | Empty state above the add form | "No expenses yet. Add the first one below." |
| No people on the trip | Trip has no people (F3) | Add form disabled, with a message above it | "Add people to the trip before adding an expense." |
| Description empty | Blank after trimming (F4.R2) | Error on the description field | "Enter a description." |
| Description too long | More than 100 characters (F4.R2) | Error on the description field | "A description can be at most 100 characters." |
| Amount empty | No amount entered (F4.R3) | Error on the amount field | "Enter an amount." |
| Amount not a number | Text that is not a decimal amount (F4.R3) | Error on the amount field | "Enter an amount like 12.50." |
| Amount zero or less | 0 or negative (F4.R3) | Error on the amount field | "Enter an amount greater than 0.00." |
| Too many decimal places | More than 2 places (F4.R3) | Error on the amount field | "An amount can have at most 2 decimal places." |
| Amount too large | Too large to store (F4.R3) | Error on the amount field | "That amount is too large to record." |
| Payer not chosen | No payer selected (F4.R4) | Error on the payer field | "Choose who paid." |
| No participants | Every person unchecked (F4.R5) | Error above the checkboxes | "Choose at least one person to split between." |
| Adding | After submit, before response | Add expense button disabled | "Adding…" |
| Expense added | Success | Expense appears at the top of the list. Form resets, with all people checked again | _(no message)_ |
| Delete confirmation | User selects the trash icon (F4.R9) | Dialog with Delete expense and Cancel | Title: "Delete {description}?" Body: "This removes the expense and changes the balances. You can't undo this." Buttons: "Delete expense", "Cancel" |
| Expense deleted | Success | Row disappears. Total and balances update | "{description} was deleted." |
| Shares dialog | User selects an expense row (F4.R10a) | Dialog listing each participant's share, with Close | Title: "Shares for {description}". Total label: "Total". Close button: "Close" (approved with the spec on 2026-10-20) |
| Expense not found | Deleted in another tab, or not on this trip | Message in the panel | "This expense no longer exists." |
| Trip not found | Request for a trip the user does not own, or a missing trip (F4.R15) | Same as F2 | "This trip no longer exists." (F2 copy) |
| Card: expense count | The trip has expenses (F4.R17) | Text on the trip card | "{n} expenses", or "1 expense" for one |
| Card: no expenses | The trip has no expenses (F4.R17) | Text on the trip card | "no expenses yet" (mockup) |
| Card: total | The trip has expenses (F4.R18) | "Total spent" label and the amount on the trip card | "Total spent" (mockup), then the amount |
| Network or server error | Request fails | Shown above the form or dialog | "Something went wrong. Please try again." (F1 copy) |

Labels and helper text from the mockup (final): "What was it for?", "Amount", "Paid by", "Split between", "Add expense", and "Split equally · leftover cents go to people in the order they were added". Placeholders from the mockup, which are examples only: "Strawberry farm tickets" and "0.00".

## Accessibility
Target: **WCAG 2.2 AA**, the project target set in F1.
- Every field has a visible label. Placeholders are examples, not labels.
- "Split between" is a group with a visible legend. Each checkbox is labelled with the person's name, so the state does not depend on colour or the check mark alone.
- The amount field uses a decimal keyboard on phones, and accepts only the number format in F4.R3.
- The "Paid by" choice is a native select, so it works with the keyboard and screen readers.
- Errors are linked to their field and announced to screen readers. The "Choose at least one person" error is linked to the checkbox group.
- Each row's delete control has an accessible name that includes the description, for example "Delete Dinner at Hill Station".
- The delete dialog traps focus while open. Escape closes it and returns focus to the trash icon that opened it.
- The icon on each row is decorative (`aria-hidden`). The description carries the meaning.
- Text and error colours meet at least 4.5:1 contrast (WCAG 1.4.3).
- Controls are at least 24×24 CSS pixels (WCAG 2.5.8).
- The card's expense count and total are text (F4.R17, F4.R18).
- No animation is needed. Reduced-motion support applies only if animation is added later.

## Edge cases
- `10` is accepted as `10.00`. `10.5` is accepted as `10.50`. `10.505` is refused (F4.R3).
- `0.01` is accepted. Split three ways, it gives 0.01 to the earliest-added participant and 0.00 to the others (F4.R7).
- A share of 0.00 is still a participant. The row shows the expense as split between them, and their balance reflects a zero share.
- An expense can be paid by someone not checked as a participant (F4.R6). That person's share is 0.00, and they receive the money back in their balance (product.md, R4).
- Unchecking everyone is refused, even if the payer is one of the people (F4.R5).
- A person added to the trip after an expense is not a participant of that expense. Shares follow the people checked when the expense was saved.
- The leftover-cent order uses the order people were added to the trip (F3.R5), not the order of the checkboxes.
- An expense deleted in another tab: opening or deleting it there shows "This expense no longer exists."
- Two identical expenses (same description, amount and payer) are both accepted. Nothing in product.md prevents it.
- A trip with one expense shows "1 expense" on its card, and its total (F4.R17, F4.R18).
- Deleting an expense can end a person's "in expenses" status (F3.R7). The tag becomes "In payments" or disappears, depending on F6.

## Acceptance criteria
- **F4.AC1** Given a trip with Sam, Alex and Jo, when I add `Dinner` for 90.00 paid by Sam with everyone checked, then the expense appears with a share of 30.00 for each person.
- **F4.AC2** Given a trip with Sam, Alex and Jo (added in that order), when I add `Snacks` for 10.00 paid by Sam with everyone checked, then the shares are Sam 3.34, Alex 3.33 and Jo 3.33, and they add up to 10.00.
- **F4.AC3** Given I enter `0`, when I submit, then I see "Enter an amount greater than 0.00." and no expense is saved. Given I enter `0.01`, then it is saved.
- **F4.AC4** Given I enter `10.505`, when I submit, then I see the decimal-places error and no expense is saved. Given I enter `10.5`, then it is saved as 10.50.
- **F4.AC5** Given I enter only spaces as the description, when I submit, then I see "Enter a description." Given I enter 101 characters, then I see the length error. Given I enter 100, then it is saved.
- **F4.AC6** Given no payer is selected, when I submit, then I see "Choose who paid." and no expense is saved.
- **F4.AC7** Given I uncheck every person, when I submit, then I see "Choose at least one person to split between." and no expense is saved.
- **F4.AC8** Given Sam pays and Sam is unchecked as a participant, when I submit, then the expense is saved, Sam's share is 0.00, and the others share the amount.
- **F4.AC9** Given I open the add form on a trip, then every person is checked and no payer is selected.
- **F4.AC10** Given a trip with no people, when I view the Expenses panel, then the add form is disabled with "Add people to the trip before adding an expense."
- **F4.AC11** Given I add expense X, then expense Y, when I view the list, then Y is above X.
- **F4.AC12** Given an expense is listed, when I select its trash icon and choose Cancel, then it remains. When I choose Delete expense, then it is removed and the total is reduced by its amount.
- **F4.AC13** Given `Riley` is a participant of the only expense that involves Riley, when I delete that expense, then Riley's row no longer shows "In expenses" and Riley can be removed (F3.AC6).
- **F4.AC14** Given user B requests user A's trip's expenses, then B gets "This trip no longer exists." (F4.R15).
- **F4.AC15** Given I am viewing an expense, then there is no control to edit it (F4.R12).
- **F4.AC16** Given a trip has expenses, when I view the Expenses panel, then the total equals the sum of every expense amount (F4.R13).
- **F4.AC17** Given an expense row, when I read it, then it shows the description, the amount, the payer and "split N ways", with no per-person share (F4.R10).
- **F4.AC18** Given an expense row, when I select it, then a dialog opens listing each participant's name and share in the order they were added, with the total. When I choose Close, the dialog closes and the trip page is unchanged (F4.R10a).

- **F4.AC19** Given a trip with three expenses, when I view the trip list, then its card shows "3 expenses" and the total (F4.R17, F4.R18).
- **F4.AC20** Given a trip with one expense, when I view the trip list, then its card shows "1 expense" (F4.R17).
- **F4.AC21** Given a trip with no expenses, when I view the trip list, then its card shows "no expenses yet" and no total line (F4.R17, F4.R18).

## Dependencies
- **Depends on:** F1 (login and ownership, F1.R9), F2 (trips, F2.R12 deletion), F3 (people, the payer and participants, F3.R5 order, F3.R7 in-use check).
- **Needed by:** F5 (balances are built from expenses, product.md R4), F6 (suggested payments follow from balances; a payment refers to a person, not an expense).
- **Build order (§8):** not in product.md, so the order given by the MVP table is used. F4 is the fourth step.
- **External services:** none.

## Test plan
- **Unit:** the equal split: shares sum to the amount, leftover cents go to the earliest-added participants (F4.AC1, F4.AC2, F4.R7).
- **Unit:** amount parsing: whole cents, at most 2 decimal places, greater than zero, no floating-point storage (F4.AC3, F4.AC4, F4.R3).
- **Unit:** description trimming and length (F4.AC5, F4.R2).
- **Unit:** form rules: payer required, at least one participant (F4.AC6, F4.AC7, F4.R4, F4.R5).
- **Unit:** payer not a participant gets a share of 0.00 (F4.AC8, F4.R6).
- **Unit:** the expense list is ordered newest first (F4.AC11, F4.R8).
- **Unit:** the total equals the sum of amounts (F4.AC16, F4.R13).
- **Unit:** the row text shows "split N ways" and no per-person share (F4.AC17, F4.R10).
- **Unit:** the shares dialog lists each participant's share in added order, with the total (F4.AC18, F4.R10a).
- **Integration:** add with defaults: all people checked, no payer selected (F4.AC9).
- **Integration:** a trip with no people refuses adding (F4.AC10).
- **Integration:** delete with Cancel keeps the expense; delete with confirm removes it and updates the total (F4.AC12).
- **Integration:** deleting the only expense involving a person clears their in-use status (F4.AC13, F3.R7).
- **Integration:** another user's trip cannot be read or changed through the expense endpoints (F4.AC14, F4.R15).
- **End-to-end:** the rows have no edit control (F4.AC15).
- **Unit:** the card's expense text: "1 expense", "3 expenses" and "no expenses yet", and the total line shown only with expenses (F4.AC19, F4.AC20, F4.AC21).
- **Integration:** the trip list's expense count and total match the expense list for the same trip (F4.R17, F4.R18).

## Open questions
- [x] Copy: the States table is approved on 2026-10-20.
- [ ] Icon keywords (F4.R11): which words in a description map to each of the four icons (transport, food and drink, lodging, activities). The examples in F4.R11 are not final. _Interim: generic icon on every row, decided 2026-10-21._
- [x] Per-person shares (F4.R10a): shown in a pop-up dialog (changed from the expanded row on 2026-10-20; see Decisions log). The dialog copy is in the States table.

## Decisions log
- 2026-10-20: How long can an expense description be? → 100 characters.
- 2026-10-20: What is the largest amount one expense can have? → No maximum beyond the database field.
- 2026-10-20: Who should be selected as the payer when someone opens the Add expense form? → No default; the user must choose. (The mockup pre-selects Sam; this spec follows the decision.)
- 2026-10-20: Should deleting an expense ask for confirmation first? → Yes, a confirmation dialog.
- 2026-10-20: In what order should the expense list show expenses? → Newest first.
- 2026-10-20: Should expenses have a category? → No categories. (The icon is covered below.)
- 2026-10-20: Should there be a maximum number of expenses on one trip? → No limit.
- 2026-10-20: Should an expense record a date? → No date. The time added is recorded for ordering.
- 2026-10-20: The mockup row shows "10.00 each". What should the row show? → "Split N ways" only.
- 2026-10-20: What icon should each expense row show? → Icon chosen from the description. The keyword list is open.
- 2026-10-20: Copy (States table) approved. → Approved as drafted.
- 2026-10-20: When nothing in the description matches an icon keyword, what icon should show? → A generic icon.
- 2026-10-20: Should the user choose the icon from a list? → Asked twice. The answer was no picker: the app picks the icon from the description, keeping the four inputs. The user's request for a choosable list is not met; if that is still wanted, product.md Goals needs a change first.
- 2026-10-20: Which icons are in the set? → Transport (car), Food and drink (fork and knife), Lodging (bed), Activities (ticket), and a generic money icon as the default.
- 2026-10-20: Where should per-person shares appear? → Expanded under the row (F4.R10a).
- 2026-10-20: Changed: how should the per-person shares appear? → A pop-up dialog, not an expanded row (F4.R10a).
- 2026-10-20: Approve the spec? → Yes. Status set to Approved. Copy approved, including the shares dialog. The icon keyword lists (F4.R11) are still open.
- 2026-10-21: Which description keywords should pick each icon? → Not decided. The user chose a generic icon on every row for now. F4.R11 changed as a result, so the spec went back to Draft.
- 2026-10-21: Approve the spec? → Yes. Status set to Approved. The icon keyword lists stay open, and the interim generic-icon rule (F4.R11) applies until they are decided.
- 2026-10-21: How should a trip card show its expenses and total? → "{n} expenses", "1 expense" for one, and "no expenses yet" for none. The total is shown only when there are expenses. Added as F4.R17–R18 and F4.AC19–AC21. This changes the approved spec, so it was set back to Draft.
- 2026-10-21: Approve the revised spec? → Yes. Status set to Approved.
