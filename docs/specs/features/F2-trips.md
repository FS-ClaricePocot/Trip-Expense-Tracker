# F2 — Trips

_Status: Approved · Last updated: 2026-10-20 · Source: product.md, MVP scope (F2 row)_

## Summary
A logged-in user creates a trip with a name, sees a list of their own trips, opens one, and deletes it. A trip is the group that F3–F6 build on, so F2 is where the shared spending starts. Each user sees only their own trips.

## User stories
- As a user, I want to create a trip with a name, so that I can start tracking a shared expense.
- As a user, I want to see all my trips in one list, so that I can find the one I need.
- As a user, I want to delete a trip I no longer need, so that my list stays clean and nothing is left behind.

## Scope
**In:**
- Create a trip with a name (F2.R1–R4)
- List your own trips, newest first (F2.R5, R7)
- Open a trip to a placeholder page with its name and a Delete button (F2.R6)
- Delete a trip after confirmation (F2.R7, R12)
- Trip list becomes the post-login page, replacing the F1 placeholder (F2.R10)
- Trip ownership checks, using the F1 guard (F2.R9)

**Out:**
- Editing a trip's name or anything else. product.md, Assumptions: "No editing."
- Other trip fields, such as dates, currency or description. The F2 row says the trip has a name only, and product.md has one currency.
- Undo after deleting a trip. R8 asks for a confirmation only.
- Adding people (F3), expenses (F4), balances (F5) and settling up (F6). The trip page shows only its name and Delete button until then.
- Totals, people and expense counts, and settle status on trip cards. These depend on F3–F6 and are added as those features are built.
- Sharing a trip with other users. product.md, After the core: out of scope.

## User flow & screens
1. **After login:** the user lands on the trip list (F2.R10).
2. **Empty list:** if the user has no trips, the empty state appears with the create form (F2.R11).
3. **Create:** the user types a name in the create form on the trip list page and submits.
   - Name is refused if it is blank or too long (F2.R2).
   - If the user already has a trip with the same name, the warning appears. The user chooses **Create anyway** or **Cancel** (F2.R3).
   - If the user has 50 trips, the create form is disabled with a message (F2.R4).
   - On success, the new trip appears at the top of the list.
4. **Open:** the user selects a trip in the list and sees the trip page with its name and a Delete button (F2.R6).
5. **Delete:** the user selects Delete. A confirmation dialog appears.
   - **Cancel** closes the dialog and leaves the trip.
   - **Delete trip** deletes the trip and everything in it. The user returns to the trip list, and the trip is gone (F2.R7).
6. **Logged out:** a request for the trip list or a trip page redirects to the log-in page (F1.R15).

Mockups (in `docs/design/mockups/`):
- **Trip list** (`Your trips.png`): the "Your trips" page with the create form ("New trip" field and "Create trip" button) above the cards. The cards in that mockup also show totals, people and expense counts, and settle status. In F2 the cards show only the trip name and its Delete action (F2.R5). The rest comes with F3–F6.
- **Delete confirmation** (`Delete trip confirmation.png`): the dialog in F2.R7, with the counts and copy below.
- **Trip page** (`Trip detail expenses, people, balances.png`, `Settle up.png`): the full layout is built by F3–F6. In F2 the trip page is the placeholder in F2.R6 (name and Delete button). The duplicate-name warning and the limit message have no mockup. They use the copy in the States table.

## Requirements
- **F2.R1** A logged-in user can create a trip by giving it a name.
- **F2.R2** The trip name is required. Spaces at the start and end are removed before the name is checked and saved. A name that is blank after trimming is refused. The trimmed name is at most 100 characters. Any characters are allowed.
- **F2.R3** Before a trip is created, the app checks the user's own trips for the same name. The check ignores capital letters, so `Paris 2026` and `paris 2026` match. If a match exists, the app shows a warning and does not create the trip yet. The user must choose **Create anyway** to proceed, or **Cancel** to create nothing. Two of the user's trips may share a name after **Create anyway**.
- **F2.R4** A user can have at most 50 trips. The count includes every trip the user owns. A create request beyond 50 is refused. Deleting a trip frees a place.
- **F2.R5** The trip list shows only the logged-in user's trips, in order from newest to oldest by creation time.
- **F2.R6** Opening a trip shows a trip page with the trip's name and a Delete button. Content is added by F3–F6.
- **F2.R7** Deleting a trip asks for confirmation in a dialog with **Delete trip** and **Cancel**. The dialog shows how many people, expenses and payments the trip holds. In F2 these counts are 0, because those features are not built yet. **Cancel** leaves the trip unchanged. **Delete trip** deletes the trip. The deletion is permanent.
- **F2.R8** A trip's name cannot be changed after creation. The app has no edit control for trips. _(product.md, Assumptions: "No editing.")_
- **F2.R9** Every trip read, create and delete is checked against the logged-in user (F1.R9). A request for a trip owned by someone else, or one that does not exist, gets the same "not found" response.
- **F2.R10** After login, the user lands on the trip list. This replaces the placeholder in F1.R16.
- **F2.R11** If the user has no trips, the trip list shows an empty state. It includes the create form.
- **F2.R12** Deleting a trip also deletes everything in it. Today that is nothing beyond the trip itself. F3–F6 add people, expenses and payments, and each of them must be removed with the trip. _(product.md, R8: "Deleting a trip deletes everything in it, after the user confirms.")_

## Data & contract
**Trip (new record):**
- `id`
- `owner_user_id`: the account that owns the trip. Required. Indexed. (F1.R9)
- `name`: trimmed text, 1–100 characters (F2.R2)
- `created_at`: used for ordering (F2.R5)

Trips are linked to their people, expenses and payments in F3–F6. Each of those specs sets how it is removed when its trip is deleted (F2.R12).

**Browser storage:** None.

**API (paths and exact shapes are decided in the plan):**
- **List trips:** returns the logged-in user's trips, newest first.
- **Create trip:** takes a name. If a same-name trip exists and the request does not confirm it, the response is a duplicate warning and nothing is created. If the request confirms it, the trip is created. Refused when the name is invalid (F2.R2) or the user is at 50 trips (F2.R4).
- **Get trip:** returns one trip the user owns. Other trips get "not found" (F2.R9).
- **Delete trip:** deletes one trip the user owns. Other trips get "not found" (F2.R9).

All four requests need a logged-in session (F1.R9).

## States & errors
Copy below is a **draft** from Claude, for your review. It becomes final once the spec is approved.

| State | When | Behavior | Copy (draft) |
|---|---|---|---|
| Loading list | The trip list is being fetched | Shows a loading indicator in the list area | "Loading your trips…" |
| Empty list | No trips yet (F2.R11) | Empty state with the create form | "You have no trips yet. Create your first trip to start tracking shared expenses." |
| Create: name empty | Blank name after trimming (F2.R2) | Error on the name field | "Enter a trip name." |
| Create: name too long | More than 100 characters (F2.R2) | Error on the name field | "A trip name can be at most 100 characters." |
| Create: duplicate name | Same name as one of the user's trips (F2.R3) | Warning above the form, with Create anyway and Cancel | "You already have a trip called '{name}'. Create another one with the same name?" |
| Create: limit reached | User has 50 trips (F2.R4) | Create form disabled, with message above it | "You have reached the limit of 50 trips. Delete a trip to create a new one." |
| Creating | After submit, before response | Submit is disabled | "Creating trip…" |
| Trip created | Success | Trip appears at the top of the list. Trip name field clears | _(no message)_ |
| Delete confirmation | User selects Delete (F2.R7) | Dialog with the counts, Delete trip and Cancel | Title: "Delete {name}?" Body: "This deletes the trip and everything in it. You can't undo this." Counts: "{people} people", "{expenses} expenses", "{payments} payment" (from the mockup; the copy is the user's design and is final) Buttons: "Delete trip", "Cancel" |
| Trip deleted | Success | Returns to the list. Trip is gone | "Trip deleted." |
| Trip not found | Opened or deleted a trip that does not exist or belongs to someone else (F2.R9) | Message on a page with a link back to the list | "This trip no longer exists." |
| Logged out | No session (F1.R15) | Redirect to the log-in page | "Please log in to see your trips." (F1 copy) |
| Network or server error | Request fails | Shown above the form or dialog | "Something went wrong. Please try again." (F1 copy) |

## Accessibility
Target: **WCAG 2.2 AA**, the project target set in F1.
- The trip name field has a visible label. Placeholder text is not used as the label.
- The create form, the duplicate warning and the limit message are linked to their fields and announced to screen readers.
- The trip list is a real list, so screen readers announce the number of items.
- The Delete button's accessible name includes the trip name, for example "Delete trip Paris 2026", so a screen reader user can tell the buttons apart.
- The delete dialog traps focus while open. Escape closes it and returns focus to the Delete button that opened it.
- The duplicate warning and the limit message are not shown by colour alone. They have text and an icon.
- Text and error colours meet at least 4.5:1 contrast (WCAG 1.4.3).
- Buttons and list items are at least 24×24 CSS pixels (WCAG 2.5.8).
- No animation is needed. Reduced-motion support applies only if animation is added later.

## Edge cases
- A name with only spaces is refused as blank (F2.R2).
- A name of exactly 100 characters after trimming is accepted. 101 is refused (F2.R2).
- Trimming applies to the saved name: `  Paris 2026  ` is stored as `Paris 2026`.
- A name differing only by capitals or surrounding spaces counts as a duplicate (F2.R3).
- After **Create anyway**, a second trip with the same name is created. Both appear in the list.
- At 49 trips, two create requests sent together must not both succeed, which would make 51. The limit is checked when the trip is saved, not only in the form (F2.R4).
- Deleting a trip frees a place towards the limit of 50 (F2.R4).
- A trip deleted in another browser tab: opening it or deleting it there shows "This trip no longer exists."
- A user can see their trips after a log-out and log-in, because they belong to the account, not the session.
- Trip ordering uses creation time. Two trips created in the same second may appear in either order.

## Acceptance criteria
- **F2.AC1** Given I am logged in, when I enter `Paris 2026` and submit, then the trip is created and appears at the top of the list.
- **F2.AC2** Given I enter only spaces as the name, when I submit, then I see "Enter a trip name." and no trip is created.
- **F2.AC3** Given I enter a name of 101 characters, when I submit, then I see the length error and no trip is created. Given I enter 100 characters, then the trip is created.
- **F2.AC4** Given I already have `Paris 2026`, when I create `paris 2026`, then I see the duplicate warning. When I choose Cancel, no trip is created. When I choose Create anyway, the trip is created.
- **F2.AC5** Given I have 50 trips, when I try to create another, then I see the limit message and no trip is created. When I delete one trip, then I can create one more.
- **F2.AC6** Given user A has trips and user B is logged in, when B views the trip list, then A's trips are not shown. When B opens a trip ID that belongs to A, then B sees "This trip no longer exists." (same as a missing trip).
- **F2.AC7** Given I create trips X, then Y, then Z, when I view the list, then it shows Z, Y, X.
- **F2.AC8** Given I open a trip, when the trip page loads, then it shows the trip's name and a Delete button.
- **F2.AC9** Given I select Delete on a trip, when I choose Cancel, then the dialog closes and the trip is still in the list.
- **F2.AC10** Given I select Delete on a trip, when I confirm with Delete trip, then the trip is removed from the list and "Trip deleted." appears.
- **F2.AC11** Given a trip has been deleted, when I open its ID, then I see "This trip no longer exists."
- **F2.AC12** Given I have no trips, when I view the list, then I see the empty state and the create form.
- **F2.AC13** Given I am viewing a trip, when I look at the page, then there is no control to rename or edit the trip.
- **F2.AC14** Given I log in, when login succeeds, then I see the trip list, not the F1 placeholder.
- **F2.AC15** Given I am logged out, when I open the trip list, then I am redirected to the log-in page.

## Dependencies
- **Depends on:** F1 (login, session, ownership check F1.R9, redirect F1.R15, post-login page F1.R16 that F2.R10 replaces).
- **Needed by:** F3 (people belong to a trip), F4 (expenses belong to a trip), F5 (balances), F6 (settle up). All of them reach a trip through F2.
- **Build order (§8):** not in product.md, so the order given by the MVP table is used. F2 is the second step.
- **External services:** none.

## Test plan
- **Unit:** name trimming, blank check and 100-character limit (F2.AC2, F2.AC3, F2.R2).
- **Unit:** duplicate check ignores capitals and surrounding spaces (F2.AC4, F2.R3).
- **Unit:** trip count check at 49, 50 and 51 trips (F2.AC5, F2.R4).
- **Unit:** trip list ordering by creation time (F2.AC7, F2.R5).
- **Integration:** create trip, then it appears at the top of the list (F2.AC1).
- **Integration:** create with a duplicate name without confirmation creates nothing; with confirmation creates the trip (F2.AC4).
- **Integration:** two simultaneous creates at 49 trips produce at most one new trip (F2.R4 race).
- **Integration:** user B cannot list, open or delete user A's trip, and gets "not found" (F2.AC6, F2.R9).
- **Integration:** delete with Cancel keeps the trip; delete with confirm removes it and its later children (F2.AC9, F2.AC10, F2.R12).
- **Integration:** opening a deleted trip ID shows "not found" (F2.AC11).
- **Integration:** logged-out list request redirects to login (F2.AC15).
- **Integration:** login lands on the trip list (F2.AC14).
- **End-to-end:** the empty state, the placeholder trip page and the absence of an edit control (F2.AC8, F2.AC12, F2.AC13).

## Open questions
- [ ] Trip card menu: the "Your trips" mockup shows a "..." menu on each card. F2 deletes a trip only from its trip page (F2.R6, F2.R7). Should the card menu also get a Delete option? Not decided; not in the approved spec.
- [x] Copy: approved with the spec on 2026-10-20.
- [x] Mock screens: resolved on 2026-10-20. The mockups are in `docs/design/mockups/`.

## Decisions log
- 2026-10-20: How long can a trip name be? → 100 characters.
- 2026-10-20: Can one user have two trips with the same name? → Allowed. The app warns when a same-name trip exists and the user must choose Create anyway to proceed.
- 2026-10-20: How should the app confirm a trip deletion? → Simple dialog with Delete and Cancel.
- 2026-10-20: What should opening a trip show in F2? → A placeholder trip page with its name and a Delete button.
- 2026-10-20: In what order should the trip list show trips? → Newest first.
- 2026-10-20: Should there be a limit on trips per user? → Yes. The number is 50 (chosen by the user).
- 2026-10-20: Is the trip name required, and should spaces at its ends be removed? → Required, with spaces at the ends removed.
- 2026-10-20: Who writes the copy? → Claude drafts it; the user reviews before approval.
- 2026-10-20: Should the duplicate-name check ignore capital letters? → Yes.
- 2026-10-20: Where should the create-trip form appear? → On the trip list page.
- 2026-10-20: Mockups added. Should the delete dialog use the mockup copy and counts? → Yes. The counts are 0 in F2 until F3–F6 exist.
- 2026-10-20: What should the trip list show in F2? → Trip name and Delete only. Totals and settle status come with F3–F6.
- 2026-10-20: Approve the spec? → Yes. Status set to Approved. Card-menu Delete question left open.
