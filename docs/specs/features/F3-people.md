# F3 — People

_Status: Approved · Last updated: 2026-10-21 · Source: product.md, MVP scope (F3 row)_

## Summary
A user adds the people who share a trip's expenses, and removes them when they are not needed. People are names only. They have no accounts, so a person cannot log in or see the trip. A person who is already part of an expense or payment cannot be removed, because that would change the balances. F4 (expenses) and F6 (settle up) depend on this list.

## User stories
- As a user, I want to add the people on a trip by name, so that I can split expenses between them.
- As a user, I want to remove someone who was added by mistake, so that the trip list is correct.
- As a user, I want to be told why I can't remove someone, so that I know they are part of a recorded expense.

## Scope
**In:**
- Add a person to a trip by name (F3.R1–R3)
- List the trip's people, in the order they were added (F3.R5)
- Remove a person with a confirmation (F3.R6)
- Block removal of a person who is in an expense or payment (F3.R7)
- People panel on the trip page, with the helper text from the mockup (F3.R12)
- People count on each trip's card in the trip list (F3.R13–R14). F2 deferred this to the feature that adds people.

**Out:**
- Renaming a person. product.md, Assumptions: "No editing."
- Person accounts, logins or invitations. product.md, Roles: "People on a trip are names only."
- Adding a person to an expense, choosing payers and participants, and the split (F4)
- Balances (F5) and suggested payments (F6). They read this list but are specified separately.
- Removing a person who is in an expense or payment. Not possible, by product.md R7.

## User flow & screens
1. The user opens a trip (F2.R6). The trip page shows a **People** panel, with a count of the people on the trip.
2. **Add:** the user types a name in the "Add a person" field and selects **Add**.
   - A blank, too long or duplicate name is refused, with a message (F3.R2, R3).
   - On success, the person is added to the end of the list.
3. **Remove:** for a person who is in no expense or payment, the user selects the remove control on their row. A confirmation dialog appears.
   - **Cancel** closes the dialog and keeps the person.
   - **Remove** removes the person from the trip.
4. **Blocked:** for a person in an expense or payment, the row shows the tag "In expenses" in place of the remove control (F3.R7). The helper text explains the rule.
5. **Empty list:** when a trip has no people, the People panel shows an empty state (F3.R12).

Mockups (in `docs/design/mockups/`):
- **Trip detail** (`Trip detail expenses, people, balances.png`): the People panel is on the right. It shows the count in the header, each person's name with either "In expenses" or the remove control, the "Add a person" field with its **Add** button, and the helper text. The panel's name rows and tags are taken from this mockup. The rest of that page (expenses, balances) belongs to F4 and F5.
- **Delete trip confirmation** (`Delete trip confirmation.png`): the confirmation dialog pattern used for trip deletion. The person removal dialog reuses that layout. Its copy is in the States table.

Screen names: trip page (People panel), remove-person dialog.

## Requirements
- **F3.R1** A user can add a person to a trip by giving the person a name.
- **F3.R2** The name is required. Spaces at the start and end are removed before the name is checked and saved. A name that is blank after trimming is refused. The trimmed name is at most 100 characters. Any characters are allowed.
- **F3.R3** Names are unique within a trip. The check ignores capital letters, so `Sam` and `sam` are the same person. A duplicate is refused. Unlike trip names (F2.R3), there is no option to create it anyway. _(product.md, Key terms: "identified by a name unique within that trip".)_
- **F3.R4** A person has no account. They cannot log in, and they do not see the trip. _(product.md, Roles.)_
- **F3.R5** People are listed in the order they were added. This order is used for the leftover-cent rule (product.md, R3). A person removed and added again goes to the end of the list.
- **F3.R6** Removing a person asks for confirmation in a dialog with **Remove** and **Cancel**. **Cancel** keeps the person. **Remove** removes them from the trip.
- **F3.R7** A person cannot be removed while they are in any expense or payment. This is checked on the server when the removal is requested, as well as in the interface. _(product.md, R7.)_
- **F3.R8** A person's name cannot be changed after it is added. _(product.md, Assumptions: "No editing.")_
- **F3.R9** A trip can have any number of people. _(Answered by the user: no limit.)_
- **F3.R10** Every people request is checked against the logged-in user and the trip owner (F1.R9). A request for a trip owned by someone else, or for a trip that does not exist, gets the same "not found" response.
- **F3.R11** Deleting a trip also deletes its people (F2.R12).
- **F3.R12** The trip page shows a People panel with the count of people, the list, the "Add a person" field and the helper text: "Names are unique on a trip. People in an expense or payment can't be removed." When the trip has no people, it shows the empty state.

- **F3.R13** Each trip card in the trip list shows the number of people on the trip: "{n} people", or "1 person" for one. A trip with no people shows "No people yet". _(Wording decided by the user on 2026-10-21. F2.R5 and the mockup's card text.)_
- **F3.R14** The card's count matches the People panel for the same trip, and it changes when people are added or removed.

## Data & contract
**Person (new record):**
- `id`
- `trip_id`: the trip the person belongs to. Required. Indexed. (F2)
- `name`: trimmed text, 1–100 characters (F3.R2)
- `added_at`: used for ordering (F3.R5). Re-adding a person creates a new record with a new time.
- Unique per trip, ignoring capitals (F3.R3). Enforced by the database, not only in the app, as with F1.R2.

**References from other features:** expenses (payer and participants, F4) and payments (payer and payee, F6) refer to a person. Each of those references blocks removal (F3.R7). The database must refuse the removal too, so a race between two requests cannot remove a person who has just been used.

**Browser storage:** None.

**API (paths and exact shapes are decided in the plan):**
- **List people:** returns the trip's people, in the order they were added, and whether each person is in an expense or payment (drives the tag in F3.R7).
- **Add person:** takes a name. Refused with a validation error for a blank or too-long name, or a duplicate (F3.R2, R3).
- **Remove person:** removes one person. Refused if the person is in an expense or payment (F3.R7). The refusal says so, and the person stays on the trip.

- **Trip list (F2):** each trip in the list also returns its people count (F3.R13).

All three requests need a logged-in session and an owned trip (F3.R10).

## States & errors
Copy below is a **draft** from Claude, for your review. It becomes final once the spec is approved. Copy marked "(mockup)" comes from the design and is final.

| State | When | Behavior | Copy |
|---|---|---|---|
| Loading list | The People panel is loading | Loading indicator in the panel | "Loading people…" |
| Empty list | No people on the trip (F3.R12) | Empty state with the add field | "No one is on this trip yet. Add the people who will share its expenses." |
| Add: name empty | Blank after trimming (F3.R2) | Error on the name field | "Enter a name." |
| Add: name too long | More than 100 characters (F3.R2) | Error on the name field | "A name can be at most 100 characters." |
| Add: duplicate | Same name as someone on the trip, ignoring capitals (F3.R3) | Error on the name field. Nothing is added | "{name} is already on this trip." |
| Adding | After submit, before response | Add button disabled | "Adding…" |
| Person added | Success | Person appears at the end of the list. Field clears | _(no message)_ |
| In an expense or payment | Row for a person in any expense or payment (F3.R7) | Tag in place of the remove control | "In expenses" (mockup). When the person is in a payment but no expense, the tag reads "In payments". _(Accepted for now; see Decisions log.)_ |
| Remove confirmation | User selects remove on a person (F3.R6) | Dialog with Remove and Cancel | Title: "Remove {name}?" Body: "They will no longer be on this trip. You can add them again later." Buttons: "Remove", "Cancel" |
| Removal refused | Server refuses because the person is in an expense or payment, for example after a race (F3.R7) | Message in the panel. Person stays | "{name} is in an expense or payment, so they can't be removed." |
| Person removed | Success | Row disappears from the list | "{name} was removed from the trip." |
| Helper text | Always shown in the panel (F3.R12) | Text under the add field | "Names are unique on a trip. People in an expense or payment can't be removed." (mockup) |
| Trip not found | People request for a trip the user does not own, or a missing trip (F3.R10) | Same as F2 | "This trip no longer exists." (F2 copy) |
| Card: people count | The trip has people (F3.R13) | Text on the trip card | "{n} people", or "1 person" for one |
| Card: no people | The trip has no people (F3.R13) | Text on the trip card | "No people yet" |
| Network or server error | Request fails | Shown above the form or dialog | "Something went wrong. Please try again." (F1 copy) |

## Accessibility
Target: **WCAG 2.2 AA**, the project target set in F1.
- The "Add a person" field has a visible label. The placeholder "Morgan" is an example, not the label.
- Errors on the add field are linked to the field and announced to screen readers.
- The People list is a real list, so screen readers announce the number of people.
- The remove control's accessible name includes the person's name, for example "Remove Sam", so a screen reader user can tell the rows apart.
- The "In expenses" tag is text. The reason it is shown is in the helper text, not in colour alone.
- The remove dialog traps focus while open. Escape closes it and returns focus to the remove control that opened it.
- Text and tag colours meet at least 4.5:1 contrast (WCAG 1.4.3).
- Controls are at least 24×24 CSS pixels (WCAG 2.5.8).
- The card's people count is text, so it does not depend on colour (F3.R13).
- No animation is needed. Reduced-motion support applies only if animation is added later.

## Edge cases
- `  Sam  ` is saved as `Sam`. `Sam` and ` sam ` are duplicates (F3.R2, R3).
- Two add requests for the same name sent together: only one person is added. The database rejects the second, and the user sees the duplicate message.
- A person removed and then added again goes to the end of the list, because the order is the time they were added (F3.R5).
- A person who is in a payment but no expense cannot be removed (F3.R7). This can happen after an expense is deleted (F4). The tag reads "In payments" in that case.
- A person in an expense cannot be removed, even when the expense is their only one (F3.R7).
- Deleting the trip deletes its people (F3.R11).
- Removing the last person on a trip is allowed, and the trip then has no people. _(Decided by the user on 2026-10-21. A trip with no people cannot have an expense, because an expense needs at least one participant, product.md R2.)_
- Both the database and the server refuse a removal of a person in use, so the interface cannot be used to bypass the rule (F3.R7).
- A trip with one person shows "1 person" on its card, not "1 people" (F3.R13).

## Acceptance criteria
- **F3.AC1** Given a trip with no people, when I add `Sam` and then `Alex`, then the list shows Sam first, then Alex.
- **F3.AC2** Given `Sam` is on the trip, when I add `sam`, then I see "sam is already on this trip." (or the name as typed) and no second person is added.
- **F3.AC3** Given I enter a name of 101 characters, when I submit, then I see the length error and no person is added. Given I enter 100 characters, then the person is added.
- **F3.AC4** Given I enter `  Jo  `, when I submit, then the person is added as `Jo`.
- **F3.AC5** Given I enter only spaces, when I submit, then I see "Enter a name." and no person is added.
- **F3.AC6** Given `Sam` is in no expense or payment, when I select remove on Sam and confirm with Remove, then Sam is removed from the list.
- **F3.AC7** Given I select remove on Sam, when I choose Cancel, then the dialog closes and Sam is still on the list.
- **F3.AC8** Given `Sam` is in an expense, when I view the People panel, then Sam's row shows "In expenses" and no remove control. Given the server is asked to remove Sam anyway, then the removal is refused and Sam stays.
- **F3.AC9** Given `Riley` is only in a payment, when I view the People panel, then Riley's row shows "In payments" and cannot be removed.
- **F3.AC10** Given I removed `Alex` and then added `Alex` again, when I view the list, then Alex is at the end.
- **F3.AC11** Given I am viewing the People panel, when I look at it, then there is no control to rename a person.
- **F3.AC12** Given a trip has no people, when I view the panel, then I see the empty state and the add field.
- **F3.AC13** Given user B requests the people of user A's trip, then B gets "This trip no longer exists." (F3.R10).
- **F3.AC14** Given a trip with people is deleted, then its people are gone and cannot be requested (F3.R11).
- **F3.AC15** Given a trip with three people, when I view the trip list, then its card shows "3 people" (F3.R13).
- **F3.AC16** Given a trip with no people, when I view the trip list, then its card shows "No people yet" (F3.R13).

## Dependencies
- **Depends on:** F1 (login and ownership, F1.R9), F2 (trips, F2.R6 trip page, F2.R12 deletion).
- **Needed by:** F4 (expenses use people as payers and participants), F5 (balances are per person), F6 (payments are between people). F3.R7 needs the expense and payment tables to exist before the "in use" check can return a real answer. Until F4 and F6 are built, the check is tested with stand-ins.
- **Build order (§8):** not in product.md, so the order given by the MVP table is used. F3 is the third step.
- **External services:** none.

## Test plan
- **Unit:** name trimming, blank check and 100-character limit (F3.AC3, F3.AC4, F3.AC5, F3.R2).
- **Unit:** duplicate check ignores capitals and surrounding spaces (F3.AC2, F3.R3).
- **Unit:** list ordering by added time, including a re-added person (F3.AC1, F3.AC10, F3.R5).
- **Unit:** the "In expenses" and "In payments" tag logic from the in-use check (F3.AC8, F3.AC9, F3.R7).
- **Integration:** add two people, then the list shows them in order (F3.AC1).
- **Integration:** two simultaneous adds of the same name produce one person (Edge cases).
- **Integration:** remove with confirmation removes an unused person; Cancel keeps them (F3.AC6, F3.AC7).
- **Integration:** removal of a person in use is refused by the server, using a stand-in reference until F4 and F6 exist (F3.AC8, F3.R7).
- **Integration:** another user's trip cannot be read or changed through the people endpoints (F3.AC13, F3.R10).
- **Integration:** deleting a trip removes its people (F3.AC14, F3.R11).
- **End-to-end:** the rename control is absent and the empty state is shown for a new trip (F3.AC11, F3.AC12).
- **Unit:** the people count wording: "1 person", "3 people" and "No people yet" (F3.AC15, F3.AC16, F3.R13).
- **Integration:** the trip list's people count matches the People panel for the same trip (F3.R14).

## Open questions
- [x] Copy: approved with the spec on 2026-10-20.
- [x] Label for a person who is in a payment but no expense: "In payments", accepted for now on 2026-10-20. The mockup shows only "In expenses", so the wording may change when the design is revised.
- [x] Removing the last person on a trip: allowed, decided on 2026-10-21 (see Decisions log). product.md still does not state it.

## Decisions log
- 2026-10-20: Should the duplicate check for person names ignore capital letters? → Yes.
- 2026-10-20: What is the longest a person's name can be? → 100 characters.
- 2026-10-20: Should there be a maximum number of people on one trip? → No limit.
- 2026-10-20: When a person is removed, should the app ask for confirmation first? → Yes, a confirmation dialog.
- 2026-10-20: Is a person's name required, and should spaces at its ends be removed? → Required, with spaces at the ends removed.
- 2026-10-20: Label for a person who is in a payment but no expense? → "In payments", for now.
- 2026-10-20: Approve the spec? → Yes. Status set to Approved. Copy approved. Removing the last person is still an open question.
- 2026-10-21: Should removing the last person on a trip be allowed? → Yes, allowed. Confirms the spec's proposal; no requirement changed, so status stays Approved.
- 2026-10-21: How should a trip card show its people? → "{n} people", "1 person" for one, and "No people yet" for none. Added as F3.R13–R14 and F3.AC15–AC16. This changes the approved spec, so it was set back to Draft.
- 2026-10-21: Approve the revised spec? → Yes. Status set to Approved.
