# F1 — Registration and login

_Status: Approved · Last updated: 2026-10-20 · Source: product.md, MVP scope (F1 row)_

## Summary
A visitor creates an account with an email address and password, then logs in and out. Every trip belongs to one account, so F1 is also the gate that stops anyone who is logged out, or logged in as someone else, from seeing a trip. Later features (F2–F6) rely on this gate.

## User stories
- As a visitor, I want to register with my email and a password, so that I can keep my own trips.
- As a user, I want to log in and out, so that only I can use my account on my device.
- As a user, I want my trips to stay hidden from other users, so that our shared spending is private to us.

## Scope
**In:**
- Register with email and password (F1.R1–R3, R13–R14)
- Log in and log out (F1.R4–R5)
- Automatic login after registration (F1.R7)
- Session that lasts until the browser closes, stored on the server (F1.R6)
- Hashed passwords (F1.R3)
- Login check and ownership check on every trip endpoint (F1.R9, R15). F1 builds the check. The trip endpoints themselves come in F2.
- A placeholder page after login, until F2 adds the trips list (F1.R16)

**Out:**
- Change password. A later feature, if wanted.
- Password reset and email verification (product.md, Assumptions)
- Login through other providers, such as Google or Facebook (product.md, Assumptions: email and password only)
- Account deletion. Not in the F1 row. Add it as its own feature if wanted.
- Limits on failed login attempts (decided for MVP, see Decisions log)
- Trip, person, expense and payment screens (F2–F6)

## User flow & screens
1. A visitor opens the app. They see the register and log in options.
2. **Register:** the visitor enters an email and a password, then submits. On success the account is created and the user is logged in straight away (see step 4).
3. **Log in:** the user enters their email and password, then submits. On success they are logged in.
4. After register or log in, the user goes to the post-login page. Until F2 exists, it is a placeholder with a log-out button (F1.R16).
5. **Log out:** the user chooses log out. Their session ends and they return to the logged-out state.
6. A logged-out visitor who opens a trip page is redirected to the log-in page (F1.R15). A logged-out request to a trip endpoint gets no trip data.

Mockups (in `docs/design/mockups/`):
- **Log in** (`Log in.png`): the log-in page, with the email and password fields and a link to create an account. Heading: "Welcome back".
- **Register** (`Register (error state).png`): the register page, with the email and password fields and a link to log in. Heading: "Create your account". The mockup shows an error state for a taken email. Its copy ("That email's already registered. Log in instead.") is **not** used, because F1.R8 requires the generic message. The error state uses the copy in the States table.
- **Post-login page:** until F2 is built, the placeholder described in F1.R16. From F2 on, the trip list (`Your trips.png`).

## Requirements
- **F1.R1** A visitor can create an account with an email address and a password.
- **F1.R2** Email addresses are unique across accounts and are matched case-insensitively (for example, `Jane@x.com` and `jane@x.com` are the same account). The uniqueness check is enforced by the database, not only in the app.
- **F1.R3** A password must be at least 8 characters. It is stored only as a one-way hash, never in plain text, and never written to logs.
- **F1.R4** A user can log in with their email and password. Login is matched with the same case rule as F1.R2.
- **F1.R5** A user can log out. Logging out ends their session.
- **F1.R6** The session ends when the browser closes. The session is kept on the server. The browser holds only a random session ID in a cookie with no fixed expiry date. Logging out deletes the server-side session. _(The browser-close rule is derived from the answer "until the browser closes". Server-side storage was chosen so that logout ends the session at once.)_
- **F1.R7** Registration logs the new user in straight away.
- **F1.R8** When registration uses an email that already exists, and when a login fails, the app shows a generic message. The message does not say whether the email has an account. Wrong password and unknown email get the same message.
- **F1.R9** Every trip endpoint requires a logged-in session. It also checks that the trip belongs to the logged-in user. A request with no session gets no trip data. A request for a trip owned by someone else gets the same response as a request for a trip that does not exist.
- **F1.R10** Login is email and password only. No email verification and no password reset.
- **F1.R11** Passwords, password hashes and session identifiers never appear in logs. _(product.md, MVP scope: "never log sensitive data".)_
- **F1.R12** Login attempts are not limited in the MVP. _(Decided by the user, see Decisions log.)_
- **F1.R13** Before the email is checked or matched, spaces at the start and end are removed. The result must contain exactly one `@`, with at least one dot after it. The app does not check that the address exists.
- **F1.R14** A password is at most 128 characters. Any characters are allowed, including spaces.
- **F1.R15** A logged-out visitor who opens a trip page is redirected to the log-in page. After logging in they land on the post-login page (F1.R16). Returning to the page they asked for is not part of F1.
- **F1.R16** Until F2 exists, the post-login page is a placeholder. It confirms the user is logged in and has a log-out button.

## Data & contract
**Account (new record):**
- `id`
- `email`: trimmed and stored in lower case, unique (F1.R2, R13)
- `password_hash`: one-way hash of the password (F1.R3)
- `created_at`

**Session (new record, server-side):**
- `id`: random, unguessable session ID. This is the value in the cookie.
- `user_id`: the account it belongs to
- `created_at`
- Deleted on logout (F1.R5) and when the browser closes, as far as the cookie goes (F1.R6). Removing expired rows is a plan detail.

**Browser storage:** one session cookie holding only the session ID, with no fixed expiry date (F1.R6). Its name and flags are decided in the plan.

**API:** three endpoints, register, log in and log out. Their paths, request bodies and response shapes are decided in the plan, because the stack is not yet written in product.md (no §4). Error responses use the generic messages from F1.R8.

## States & errors
Copy below was drafted by Claude and approved with the spec on 2026-10-20.

| State | When | Behavior | Copy (draft) |
|---|---|---|---|
| Submitting | After the user submits register or log in | Submit is disabled until the response arrives | "Creating your account…" / "Logging in…" |
| Password too short | Register with fewer than 8 characters | Shown on the password field | "Your password must be at least 8 characters." |
| Password too long | More than 128 characters | Shown on the password field | "Your password can be at most 128 characters." |
| Email empty | No email entered | Shown on the email field | "Enter your email address." |
| Email not valid | Fails F1.R13 | Shown on the email field | "Enter an email address like name@example.com." |
| Registration failed | Register with an existing email (any letter case), or other refusal | Generic message (F1.R8), shown above the form | "We couldn't create your account with these details." |
| Login failed | Unknown email or wrong password | Generic message (F1.R8), shown above the form | "Email or password is incorrect." |
| Logged out, trip page opened | No session (F1.R15) | Redirect to the log-in page, with a message | "Please log in to see your trips." |
| Logged out, trip endpoint called | No session (F1.R9) | No trip data; the response status is a plan detail | _(no user-facing copy; the page does not render)_ |
| Network or server error | Request fails | Shown above the form | "Something went wrong. Please try again." |
| Post-login placeholder | Logged in, F2 not built (F1.R16) | Placeholder page with a log-out button | "You're logged in. Your trips will appear here." Button: "Log out" |

Empty states do not apply to F1, because no lists are shown.

## Accessibility
Target: **WCAG 2.2 AA**, as chosen in the decisions log. The items below are how F1 meets it.
- Every input has a visible label. Placeholder text is not used as the label.
- The whole flow works with the keyboard alone, in logical tab order.
- Errors are linked to their field and announced to screen readers.
- Focus is visible on every control.
- Text and error colours meet at least 4.5:1 contrast for normal text (WCAG 1.4.3).
- No animation is needed. Reduced-motion support applies only if animation is added later.

## Edge cases
- Registering with an email that differs only in letter case from an existing one is refused with the generic message (F1.R2, F1.R8).
- An email with spaces around it is trimmed, then matched (F1.R13).
- Two registrations with the same email at the same moment: only one account is created. The database rejects the second, and the user gets the generic message.
- A password of exactly 8 characters is accepted. A password of exactly 128 characters is accepted (F1.R14).
- Logging out while already logged out does nothing and is not an error. _(Proposed, not stated.)_
- Failed logins are not counted or blocked. A user can keep trying (F1.R12). This is an accepted risk for a training app.
- A session from one browser does not log in another browser. Each browser has its own session.
- A trip owned by someone else and a trip that does not exist look the same to the user (F1.R9).

## Acceptance criteria
- **F1.AC1** Given I am logged out, when I open a trip page, then I am redirected to the log-in page, and when I request a trip endpoint, I get no trip data.
- **F1.AC2** Given I enter a new email and a password of 8 or more characters, when I submit register, then an account is created and I am logged in.
- **F1.AC3** Given an account exists for `anna@x.com`, when I register `Anna@X.com`, then I see the generic message and no second account is created.
- **F1.AC4** Given I enter a password of 7 characters, when I submit register, then I see a password error and no account is created.
- **F1.AC5** Given an account exists, when I log in with the right email and a wrong password, then I see the same generic message as for an unknown email.
- **F1.AC6** Given I am logged in, when I log out, then my session ends and trip endpoints refuse my requests.
- **F1.AC7** Given user A owns trip T, when user B requests T, then B gets the same response as for a trip that does not exist, with no data about T.
- **F1.AC8** Given I am logged in, when I close the browser and open the app again, then I am logged out.
- **F1.AC9** Given an account is stored, when I look at the database, then the password is not stored in plain text.
- **F1.AC10** Given I register or log in, when I check the logs, then no password, password hash or session identifier appears.
- **F1.AC11** Given I register with ` Anna@x.com ` (with spaces), when I submit, then the email is stored as `anna@x.com`. Given I enter `anna.x.com` (no `@`), when I submit, then I see the email error.
- **F1.AC12** Given I enter a password of 129 characters, when I submit register, then I see a password error and no account is created.
- **F1.AC13** Given I am logged in and I log out, when I then request the post-login page, then I am redirected to the log-in page.

## Dependencies
- **Depends on:** nothing. F1 is the first build step.
- **Needed by:** F2 (trips) uses the ownership check from F1.R9. F3–F6 reach trips only through F2 and so depend on F1 too.
- **Build order (§8):** not in product.md, so the order given by the MVP table is used. F1 comes first.
- **External services:** none. No email is sent (product.md, Assumptions).

## Test plan
- **Unit:** email trimming, lower-casing and format check (F1.AC3, F1.AC11, F1.R13).
- **Unit:** password length check at 7, 8, 128 and 129 characters (F1.AC4, F1.AC12).
- **Unit:** password hashing: the stored value is not the input, and it verifies correctly (F1.AC9).
- **Unit:** generic error returns the same response for wrong password and unknown email (F1.AC5, F1.R8).
- **Unit:** session cookie holds only the session ID and has no fixed expiry date (F1.AC8).
- **Unit:** logging helper removes passwords, hashes and session identifiers (F1.AC10).
- **Integration:** register, then the user is logged in (F1.AC2).
- **Integration:** logout deletes the server-side session, and the guarded test endpoint refuses the request (F1.AC6, F1.AC1, F1.AC13).
- **Integration:** an unauthenticated trip page request redirects to the log-in page (F1.AC1, F1.R15).
- **Integration:** a guarded test endpoint returns the same response for another user's record as for a missing one (F1.AC7). F1 can only test this against a placeholder endpoint. The real trip endpoints arrive in F2, which retests this.

## Open questions
None. Mockups were added to `docs/design/mockups/` on 2026-10-20.

## Decisions log
- 2026-10-20: How strong should passwords be? → Minimum 8 characters only, no other rules.
- 2026-10-20: How long should a login last? → Until the browser closes.
- 2026-10-20: What should the app say for a taken email and a failed login? → Generic messages for both.
- 2026-10-20: Should repeated failed logins be limited? → No limit in the MVP.
- 2026-10-20: After registering, should the user be logged in straight away? → Yes, log in straight away.
- 2026-10-20: Should a logged-in user be able to change their password in F1? → Out of scope for F1.
- 2026-10-20: How should email addresses be matched? → Case-insensitive.
- 2026-10-20: Mockups added to `docs/design/mockups/`. The register error-state mockup says "That email's already registered. Log in instead." Should it replace the generic message? → No. F1.R8 stands, so the generic message is used. F1 stays Approved; no requirement changed.
- 2026-10-20: Where should the server keep the session? → Server-side session in the database; the cookie holds only a random ID.
- 2026-10-20: What counts as a valid email, and should spaces be trimmed? → Trim, then check for one `@` and a dot in the domain.
- 2026-10-20: Is there a maximum password length, and which characters are allowed? → Maximum 128 characters, any characters.
- 2026-10-20: When a logged-out visitor opens a trip page, what should happen? → Redirect to the log-in page.
- 2026-10-20: When a user asks for someone else's trip, what should the response say? → Not found (same as a missing trip).
- 2026-10-20: What should the post-login page show until F2 exists? → Empty placeholder page with a log-out button.
- 2026-10-20: Which accessibility level should the app meet? → WCAG 2.2 AA.
- 2026-10-20: Who writes the copy? → Claude drafts it; the user reviews before approval.
- 2026-10-20: Approve the spec? → Yes. Status set to Approved. Mock screens to be added later.
