# F1 — Registration and login: Implementation plan

_Status: Approved · Last updated: 2026-10-21 · Spec: [F1-registration-and-login](../specs/features/F1-registration-and-login.md) (Approved, last updated 2026-10-20)_

## Overview
F1 is the first build step (F1 → F6). It scaffolds the back-end solution, the database and the Angular app, then builds register, log in and log out with server-side sessions. It also makes every endpoint require a login by default and sets up the ownership-check pattern that F2 uses for trips. The post-login page is a placeholder that F2 replaces.

## Prerequisites
- **Dependencies:** none. F1 depends on no other feature. F2–F6 depend on F1. Nothing is built yet: `app/` is empty.
- **Setup:**
  - .NET SDK 10.0.401 is installed (the current LTS). Node 24 is installed. The Angular CLI is not installed. T10 runs it through `npx`.
  - Docker is needed for PostgreSQL in local development and for Testcontainers in the integration tests. Check it is running before T2.
  - Secrets come from environment variables only. T1 adds `app/.env.example` with placeholders. The real `app/.env` is git-ignored.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | `app/src/api/TripExpenseTracker.Data/Migrations/0001_create_users_and_sessions.sql` | `users` and `sessions` tables, unique index on `lower(email)` |
| `TripExpenseTracker.Core` | `Auth/` (email, password policy, models, `IUserDao`, `ISessionDao`, `IPasswordHasher`, `AuthService`) | Pure validation and auth logic. No I/O. |
| `TripExpenseTracker.Data` | `IDbConnectionFactory`, `IUnitOfWork`, `UserDao`, `SessionDao`, `MigrationRunner` | Dapper DAOs, one transaction for register + session |
| `TripExpenseTracker.Api` | `Program.cs`, `AuthController`, session authentication handler, `PasswordHasherAdapter`, error-handling middleware, logging redaction, `SessionCleanupService` | Endpoints, cookie, default-deny authorization, ProblemDetails |
| Angular (`app/src/web/src/app/`) | `core/` (HTTP, `AuthService`, guard, error mapping), `auth/` (login and register pages), `trips/` (placeholder page) | Pages, routing, guard, copy from the spec |
| Tests | `TripExpenseTracker.Tests`, `TripExpenseTracker.IntegrationTests`, `app/src/web` specs, `app/e2e` | Unit, integration, component and Playwright tests |

Rules that are easy to break here:
- **Parameterized SQL only.** Every query in `UserDao` and `SessionDao` uses Dapper parameters.
- **One transaction.** Register creates the user and the session in one transaction (CLAUDE.md, Architecture rules).
- **No sensitive logging.** Never log passwords, hashes, session IDs, the `Cookie` header or the `Set-Cookie` header (F1.R11).
- **Time.** All time comes from an injected `TimeProvider`, and everything is stored in UTC. Tests never read the real clock.
- **No `Console` or `console.log`.** Use `ILogger` with Serilog.

## Contract changes
- **Database** (`0001_create_users_and_sessions.sql`):
  ```sql
  CREATE TABLE users (
    id            uuid PRIMARY KEY,
    email         text NOT NULL,            -- trimmed, stored lower case (F1.R13)
    password_hash text NOT NULL,
    created_at    timestamptz NOT NULL
  );
  CREATE UNIQUE INDEX ux_users_email_lower ON users (lower(email));   -- F1.R2

  CREATE TABLE sessions (
    id         text PRIMARY KEY,            -- random 32 bytes, base64url; the cookie value
    user_id    uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL
  );
  CREATE INDEX ix_sessions_user_id ON sessions (user_id);
  CREATE INDEX ix_sessions_created_at ON sessions (created_at);
  ```
  `created_at` is set by the app from `TimeProvider`, not by a database default, so tests control it.
- **API** (JSON, all under `/api/auth`):
  | Method and path | Auth | Request | Success | Failure |
  |---|---|---|---|---|
  | `POST /api/auth/register` | anonymous | `{ email, password }` | `201` `{ id, email }` and the session cookie | `400` validation (field codes, below). `400` generic registration failure (taken email or other refusal, F1.R8). |
  | `POST /api/auth/login` | anonymous | `{ email, password }` | `200` `{ id, email }` and the session cookie | `401` generic failure (unknown email and wrong password look the same, F1.R8) |
  | `POST /api/auth/logout` | anonymous | none | `204`. Deletes the server session and clears the cookie. Also `204` when there is no session (spec, Edge cases). | none |
  | `GET /api/auth/me` | required | none | `200` `{ id, email }` | `401` |
  - Validation errors use `ValidationProblemDetails` with a **code** per field, not copy: `email_required`, `email_invalid`, `password_too_short`, `password_too_long`. The front end maps the codes to the spec's copy, so copy lives in one place.
  - The generic failures are `ProblemDetails` with `type` `registration-failed` or `invalid-credentials` and no detail about the cause. The UI shows its own copy.
  - A request with no valid session to a protected endpoint returns `401` ProblemDetails with no body data. This settles the spec's "response status is a plan detail".
  - A trip the user does not own, and a missing trip, both return `404` (F1.R9). F1 builds the pattern in T7. F2 adds the real endpoints.
- **Browser storage:** one cookie, `tet_session`. It holds only the session ID. `HttpOnly`, `SameSite=Lax`, `Path=/`, `Secure` outside Development, and **no** `Expires` or `Max-Age` (F1.R6). No `localStorage` or `sessionStorage`.

## Tasks

### F1.T1 — Scaffold the back-end solution and local database
- **Files:** `app/src/api/TripExpenseTracker.sln`; projects `TripExpenseTracker.Api`, `.Core`, `.Data`, `.Tests`, `.IntegrationTests`; `app/docker-compose.yml`; `app/.env.example`; `app/.gitignore`; `app/src/api/Directory.Build.props`.
- **Change:** create the five projects with the references from CLAUDE.md (Api → Core and Data; Data → Core; tests → what they test). Turn on nullable reference types and warnings as errors in `Directory.Build.props`. Add Serilog to `Program.cs` (console sink through Serilog only, no `Console` calls), the ProblemDetails error-handling middleware, and `GET /api/health`. Add `docker-compose.yml` with a PostgreSQL service whose password comes from `.env`. `.env` and build output are git-ignored.
- **Covers:** groundwork for all requirements. No R or AC directly.
- **Tests:** one smoke test in `IntegrationTests` that starts the app with `WebApplicationFactory` and calls `/api/health`. One unit test that proves the error middleware returns ProblemDetails for an unhandled exception without leaking the stack trace.
- **Done when:** `dotnet build` and `dotnet test` pass in `app/src/api`. `docker compose up` starts PostgreSQL. No secret appears in any tracked file.
- _Needs human review: environment file and compose config._

### F1.T2 — Migration runner and the `0001` migration
- **Files:** `TripExpenseTracker.Data/Migrations/0001_create_users_and_sessions.sql` (embedded resource), `MigrationRunner.cs`; wiring in `Program.cs`; `IntegrationTests/Support/PostgresFixture.cs`.
- **Change:** add DbUp with the SQL from Contract changes. `MigrationRunner` runs when `Database:MigrateOnStartup` is `true`. It is `true` in Development and in tests and `false` otherwise, so nothing migrates a production database by accident. `PostgresFixture` starts a Testcontainers PostgreSQL container, runs the migrations, and gives each test class a clean database.
- **Covers:** F1.R2 (database-enforced uniqueness), F1.AC9 (hash column, no plain-text column).
- **Tests:** integration: after migration, both tables and the unique index exist. Inserting `Anna@x.com` and then `anna@x.com` fails with a unique violation. Running the migrations twice changes nothing.
- **Done when:** the tests pass against a fresh container, and the script runs unchanged on the compose database.
- _Needs human review: database migration._

### F1.T3 — Core: email and password rules, models, DAO interfaces
- **Files:** `TripExpenseTracker.Core/Auth/EmailAddress.cs`, `PasswordPolicy.cs`, `User.cs`, `Session.cs`, `IUserDao.cs`, `ISessionDao.cs`, `IPasswordHasher.cs`, `RegistrationValidator.cs`.
- **Change:** pure logic and interfaces, no I/O.
  - `EmailAddress.Normalize` trims, then lower-cases with the invariant culture (F1.R13, F1.R2).
  - The email check requires a non-empty value with exactly one `@` and at least one `.` after it. It does no more than that (spec, F1.R13).
  - `PasswordPolicy` accepts 8 to 128 characters. A character means a Unicode code point, not a UTF-16 unit, so one emoji counts as one (F1.R3, F1.R14). Any characters are allowed, including spaces. A password is never trimmed.
  - `RegistrationValidator` returns the field codes from Contract changes.
- **Covers:** F1.R2, F1.R3, F1.R13, F1.R14, F1.AC4, F1.AC11, F1.AC12.
- **Tests:** unit (xUnit, FluentAssertions): password length at 7, 8, 128 and 129; a 128-character password made of emoji; spaces inside a password; email trimming and lower-casing (`" Anna@x.com "` becomes `anna@x.com`); `anna.x.com` has no `@`; two `@`; no dot after the `@`; empty email.
- **Done when:** all the unit tests pass and `Core` has no reference to `Data`, Npgsql or ASP.NET.

### F1.T4 — Data: connection factory, unit of work, user and session DAOs
- **Files:** `TripExpenseTracker.Data/IDbConnectionFactory.cs`, `NpgsqlConnectionFactory.cs`, `IUnitOfWork.cs`, `UnitOfWork.cs`, `UserDao.cs`, `SessionDao.cs`; DI registration.
- **Change:** `UserDao`: insert user (returns "email taken" on the unique violation, never throws it to callers), find by email (case-insensitive through `lower(email) = @email`), find by id. `SessionDao`: insert, find by id, delete by id, delete older than a cutoff. The unit of work wraps a connection and transaction. Both DAOs join it when one is active. All SQL is parameterized.
- **Covers:** F1.R2, F1.AC3 (no second account), F1.AC9, and the concurrency edge case.
- **Tests:** integration (Testcontainers): insert and find round trip; `Anna@x.com` then `anna@x.com` reports "email taken"; 20 parallel inserts of the same email give exactly one row; a rolled-back unit of work leaves no user and no session; a stored row never equals the plain password.
- **Done when:** the integration tests pass, and a search of `Data` finds no string-concatenated SQL.

### F1.T5 — Core: `AuthService` with hashing and session rules
- **Files:** `TripExpenseTracker.Core/Auth/AuthService.cs`, `SessionIdGenerator.cs`, `AuthOptions.cs`; `TripExpenseTracker.Api/Auth/PasswordHasherAdapter.cs`; package `Microsoft.Extensions.Identity.Core`.
- **Change:**
  - `AuthService` has `RegisterAsync`, `LoginAsync`, `LogoutAsync` and `ResolveSessionAsync`. Register validates, hashes, then creates the user and the session in one unit of work (F1.R7).
  - Login for an unknown email still runs one hash verification against a fixed dummy hash, so timing does not reveal whether the email exists (F1.R8). A password longer than 128 characters fails as a normal login failure without hashing.
  - The session ID is 32 bytes from `RandomNumberGenerator`, base64url encoded.
  - `ResolveSessionAsync` rejects and deletes a session older than `Auth:SessionMaxAge`, **24 hours** by default (Decisions log). It reads `TimeProvider`.
  - `IPasswordHasher` lives in Core. `PasswordHasherAdapter` in Api wraps ASP.NET `PasswordHasher<T>` (PBKDF2).
- **Covers:** F1.R1, F1.R3, F1.R4, F1.R5, F1.R6 (server side), F1.R7, F1.R8, F1.AC2, F1.AC5, F1.AC9.
- **Tests:** unit with fake DAOs and `FakeTimeProvider`: the stored hash is not the password and it verifies; the same message for a wrong password and an unknown email; the dummy verification runs on the unknown-email path; two logins give different session IDs; a session at 23h59m resolves and at 24h01m is rejected and deleted; logout deletes the session; logout with no session does nothing and does not fail.
- **Done when:** the unit tests pass and no test reads the real clock.
- _Needs human review: authentication._

### F1.T6 — Api: session authentication, cookie and the four endpoints
- **Files:** `Api/Auth/SessionAuthenticationHandler.cs`, `SessionCookie.cs`, `Controllers/AuthController.cs`, request and response records; `Program.cs`.
- **Change:**
  - A custom authentication scheme reads `tet_session`, calls `ResolveSessionAsync` and builds the user principal. It does not use the encrypted ASP.NET auth cookie, so the cookie holds only the session ID (F1.R6).
  - `SessionCookie` sets the flags from Contract changes. There is no `Expires`.
  - A fallback authorization policy requires a logged-in user on every endpoint. `[AllowAnonymous]` appears only on register, login, logout and health. A new endpoint is protected unless someone opts it out.
  - Challenge and forbid return `401` ProblemDetails, never a redirect.
  - Add the four endpoints from Contract changes. Responses carry no list of which field was wrong beyond the codes above.
  - There is no rate limiting or lockout (F1.R12). No verification or reset endpoint exists (F1.R10).
- **Covers:** F1.R1, F1.R2, F1.R4–R8, F1.R10, F1.R12, F1.AC2, F1.AC3, F1.AC4, F1.AC5, F1.AC6, F1.AC8 (cookie has no expiry), F1.AC11, F1.AC12.
- **Tests:** integration (`WebApplicationFactory` + Testcontainers, `FakeTimeProvider`):
  - register logs the user in: `Set-Cookie` is present, and `GET /api/auth/me` with it returns `200` (F1.AC2);
  - register with `Anna@X.com` after `anna@x.com` gives the generic `400` and the user count stays 1 (F1.AC3);
  - 7 and 129 characters give field codes and create no user (F1.AC4, F1.AC12);
  - ` Anna@x.com ` is stored as `anna@x.com`, and `anna.x.com` gives `email_invalid` (F1.AC11);
  - a wrong password and an unknown email return identical status, `type` and body (F1.AC5);
  - the cookie is `HttpOnly`, `SameSite=Lax`, has no `Expires` or `Max-Age`, and its value equals the row in `sessions` and nothing else (F1.AC8);
  - after logout, `/api/auth/me` returns `401` and the row is gone (F1.AC6);
  - 20 wrong logins in a row never block a correct login (F1.R12);
  - `GET /api/auth/me` without a cookie returns `401`.
- **Done when:** the integration tests pass and the cookie flags are asserted in a test, not only in code.
- _Needs human review: authentication and cookie handling._

### F1.T7 — Ownership-check pattern and the protected-by-default test
- **Files:** `Core/Auth/OwnedResource` helper or `ICurrentUser`; `Api/Auth/CurrentUser.cs`; `IntegrationTests/Support/OwnershipProbeController.cs` (test project only).
- **Change:** add `ICurrentUser` (the logged-in user's id) and write down in a short code comment the pattern F2 follows: the owner id goes in the DAO's `WHERE` clause, and "not yours" and "missing" both return `404` (F1.R9, CLAUDE.md). F1 has no trip table, so the probe is a test-only controller registered by the test factory. It serves a small in-memory record owned by user A. It uses the same pattern, and it is **not** part of the shipped Api.
- **Covers:** F1.R9, F1.AC1 (API half), F1.AC7.
- **Tests:** integration: with no cookie, the probe returns `401` and no data (F1.AC1); user B requesting A's record gets a response with the same status and body as a request for an id that does not exist (F1.AC7); after logout, user A's own request returns `401` (F1.AC6). F2 repeats AC7 against the real trip endpoints (spec, Test plan).
- **Done when:** the three tests pass, and the probe code is absent from the `Api` project.
- _Needs human review: authorization and ownership._

### F1.T8 — Logging redaction
- **Files:** `Api/Logging/SensitiveDataRedactor.cs`, `Serilog` setup in `Program.cs`; `IntegrationTests/Logging/LogCaptureTests.cs`.
- **Change:** Serilog configuration masks any log property named like `password`, `passwordHash`, `sessionId`, `token`, `cookie` or `authorization` with `[REDACTED]`. Request logging records the method, path and status only, never headers or bodies. No code path logs a session ID. A failed login logs the event without the email's password and without saying whether the email exists.
- **Covers:** F1.R3 (never logged), F1.R11, F1.AC10.
- **Tests:** unit: the redactor masks each listed property name, case-insensitively, including inside a structured object. Integration with an in-memory Serilog sink: register, log in and log out with known values, then assert that no captured event contains the password, the stored hash or the session ID (F1.AC10).
- **Done when:** both tests pass. A repo-wide search finds no `Console.` or `console.log` calls.

### F1.T9 — Expired-session cleanup
- **Files:** `Api/Auth/SessionCleanupService.cs`; `Auth:SessionCleanupInterval` setting (default 1 hour).
- **Change:** a hosted `BackgroundService` that deletes sessions older than `Auth:SessionMaxAge`, driven by `TimeProvider`. It logs only the number of rows removed. A failure is logged and the service keeps running (project rules: do not swallow errors).
- **Covers:** F1.R6 (spec: "removing expired rows is a plan detail").
- **Tests:** integration with `FakeTimeProvider`: a 25-hour-old session is deleted by one cleanup run and a 1-hour-old session stays. Unit: a thrown error is logged and the loop continues.
- **Done when:** the tests pass and the service does not run in the test host unless a test starts it.

### F1.T10 — Angular scaffold, proxy and the `core/` layer
- **Files:** `app/src/web/` (generated), `proxy.conf.json`, `core/http/api.interceptor.ts`, `core/auth/auth.service.ts`, `core/auth/auth.guard.ts`, `core/errors/problem-mapper.ts`, `app.routes.ts`, `app.config.ts`.
- **Change:** generate the standalone Angular app in `app/src/web` with strict TypeScript, SCSS and the test runner the CLI provides out of the box (Karma/Jasmine or Jest, as CLAUDE.md allows). Add Angular CDK. `proxy.conf.json` forwards `/api` to the API, so there is no CORS. The HTTP layer sends credentials and, on a `401` from a non-auth endpoint, clears the signed-in state and goes to `/login`. `AuthService` holds `status` (`unknown`, `anonymous`, `authenticated`) and `user` as signals, and loads state from `GET /api/auth/me`. The guard waits for that load and redirects a logged-out visitor to `/login` with the "Please log in to see your trips." message (F1.R15). `problem-mapper.ts` turns the field codes into the spec's copy.
- **Covers:** F1.R13 and F1.R14 on the client (mirrors the server), F1.R15, F1.AC1 (front-end half).
- **Tests:** front end (Angular Testing Library): the guard redirects when anonymous and allows when authenticated; `AuthService` moves `unknown` → `anonymous` on a `401`; the problem mapper returns the exact copy for each code; the interceptor sends credentials.
- **Done when:** `ng build` and the unit tests pass, and `ng serve` proxies `/api/health`.

### F1.T11 — Register page
- **Files:** `app/src/web/src/app/auth/register/register.component.{ts,html,scss}`, spec file.
- **Change:** a Reactive Form with a visible Email label and Password label, heading "Create your account", and a link to log in (mockup `F1 · Register (error state).png`). Client checks mirror F1.R3, F1.R13 and F1.R14 and show the copy from the spec's States & errors table. The submit button reads "Create account" and, while sending, is disabled and shows "Creating your account…". The generic failure ("We couldn't create your account with these details.") shows above the form with an alert icon and text, as the current mockup draws it, and no copy says the email is already registered. The mockup's static text is included as drawn: the subtitle "Just an email and a password." and the line "Your trips are private. Only you can see them." Errors are linked to their fields with `aria-describedby` and announced through a live region. Success goes to `/trips`.
- **Covers:** F1.R1, F1.R3, F1.R8, F1.R13, F1.R14, F1.AC2, F1.AC3, F1.AC4, F1.AC11, F1.AC12; spec, Accessibility.
- **Tests:** front end: each validation message appears for empty email, `anna.x.com`, 7 and 129 characters; 8 and 128 are accepted; the submit button is disabled while the request runs; the generic message appears on a `400` generic failure, and no text says the email is already registered; success navigates to `/trips`; the whole form works with keyboard only.
- **Done when:** the component tests pass and the page matches the spec copy word for word.

### F1.T12 — Log in page
- **Files:** `app/src/web/src/app/auth/login/login.component.{ts,html,scss}`, spec file.
- **Change:** heading "Welcome back", Email and Password fields with visible labels, a link to create an account (mockup `F1 · Log in.png`), and the subtitle "Log in to pick up where your trips left off." as drawn. The submit button reads "Log in" and, while sending, is disabled and shows "Logging in…". A `401` shows "Email or password is incorrect." above the form. A network or server error shows "Something went wrong. Please try again." When the guard sent the user here, "Please log in to see your trips." shows. Success goes to `/trips` (F1.R15: no return to the earlier page). The mockup's static text (the tagline and the line "Your trips are private. Only you can see them.") is included as drawn.
- **Covers:** F1.R4, F1.R8, F1.R15, F1.AC5; spec, Accessibility.
- **Tests:** front end: the generic message is identical for a wrong password and an unknown email; the redirect message shows when the guard sent the user; the network error message shows on a `500`; focus order and labels pass role queries.
- **Done when:** the component tests pass.

### F1.T13 — Post-login placeholder, log out and routing
- **Files:** `app/src/web/src/app/trips/trips-placeholder.component.*`, `app.routes.ts`, shared header component with the log-out button.
- **Change:** a guarded `/trips` route shows the placeholder: "You're logged in. Your trips will appear here." and a **Log out** button (F1.R16). The wildcard route redirects to `/trips`, so a logged-out visit to `/trips/123` goes `**` → `/trips` → guard → `/login`. Log out calls `POST /api/auth/logout`, clears the signals and goes to `/login`. After logout, `/trips` redirects to `/login` (F1.AC13).
- **Covers:** F1.R5, F1.R15, F1.R16, F1.AC1, F1.AC6, F1.AC13.
- **Tests:** front end: the placeholder shows the exact copy and the button; log out clears state and navigates; a logged-out `/trips` and `/trips/123` both land on `/login`.
- **Done when:** the tests pass. The placeholder is easy for F2 to replace (one route, one component).

### F1.T14 — Serve the built Angular app from ASP.NET
- **Files:** `Api/Program.cs`, `Api/TripExpenseTracker.Api.csproj` (static files from the Angular `dist` output).
- **Change:** in production the API serves the built Angular files and falls back to `index.html` for any non-`/api` route (CLAUDE.md, Architecture rules). `/api/*` is never rewritten, so an unknown API path still returns `404`. The cookie `Secure` flag is on outside Development.
- **Covers:** F1.R6 (Secure flag in production), groundwork for deployment.
- **Tests:** integration: with a stub `wwwroot/index.html`, `GET /trips` returns `index.html`; `GET /api/nope` returns `404` ProblemDetails; the production configuration sets the `Secure` flag on the cookie.
- **Done when:** the tests pass. Nothing is deployed and no deployment pipeline is added.
- _Needs human review: deployment-related configuration._

### F1.T15 — End-to-end tests
- **Files:** `app/e2e/` (Playwright config and specs), a script that starts the compose database, the API and the web app on a test database.
- **Change:** Playwright runs against a real database that is not shared or production. Each test registers its own unique email.
- **Covers:** F1.AC1, F1.AC2, F1.AC3, F1.AC5, F1.AC6, F1.AC13.
- **Tests:** register, then see the placeholder, then log out and land on the log-in page; opening `/trips` and `/trips/abc` logged out redirects with the message; a duplicate email with different capitals shows the generic message; a wrong password shows "Email or password is incorrect."; after logout, going back to `/trips` redirects.
- **Done when:** the Playwright suite passes headless against the local stack with no shared state between tests.

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F1.R1 | T5, T6, T11 | AC2 | T6 register logs in; T11 form success; T15 register flow |
| F1.R2 | T2, T3, T4, T6 | AC3 | T2 unique index; T4 parallel inserts; T6 case-variant register |
| F1.R3 | T3, T5, T8, T11 | AC4, AC9 | T3 length cases; T5 hash is not the password; T8 no password in logs |
| F1.R4 | T5, T6, T12 | AC5 | T6 login; T12 login form |
| F1.R5 | T5, T6, T13 | AC6 | T6 logout deletes the row; T13 logout clears state |
| F1.R6 | T5, T6, T9, T14 | AC8 | T6 cookie has no `Expires`; T5 24h cap; T9 cleanup; T14 `Secure` in production |
| F1.R7 | T5, T6 | AC2 | T6 register returns the cookie; T4 rollback test |
| F1.R8 | T5, T6, T11, T12 | AC3, AC5 | T6 identical failure bodies; T11 and T12 generic copy |
| F1.R9 | T6, T7 | AC1, AC7 | T7 probe: `401` and same-as-missing |
| F1.R10 | T6 | none | T6 only the four endpoints exist and accept email and password only |
| F1.R11 | T8 | AC10 | T8 redactor unit test and log-capture integration test |
| F1.R12 | T6 | none | T6 20 failures then a correct login succeeds |
| F1.R13 | T3, T10, T11 | AC11 | T3 trim and `@` rules; T6 stored lower case; T11 email errors |
| F1.R14 | T3, T10, T11 | AC12 | T3 128 and 129; T11 password-too-long message |
| F1.R15 | T10, T12, T13 | AC1, AC13 | T10 guard; T13 `/trips` and `/trips/123`; T15 redirect |
| F1.R16 | T13 | none | T13 placeholder copy and button |

| Acceptance criterion | Tasks | Test |
|---|---|---|
| F1.AC1 | T7, T10, T13, T15 | T7 probe returns `401` with no cookie; T13 guard redirect; T15 e2e |
| F1.AC2 | T5, T6, T11, T15 | T6 register then `me` returns `200`; T15 |
| F1.AC3 | T4, T6, T15 | T4 and T6 case-variant register; T15 |
| F1.AC4 | T3, T6, T11 | T3 length 7; T6 `password_too_short`, no user |
| F1.AC5 | T5, T6, T12, T15 | T6 identical bodies; T12; T15 |
| F1.AC6 | T6, T7, T13, T15 | T6 `me` is `401` after logout; T7 |
| F1.AC7 | T7 | T7 other user's record equals a missing record; F2 repeats it on real trips |
| F1.AC8 | T6 | T6 cookie attributes (no `Expires` or `Max-Age`) |
| F1.AC9 | T2, T4, T5 | T4 stored value is not the password; T5 hash check |
| F1.AC10 | T8 | T8 log-capture test |
| F1.AC11 | T3, T6, T11 | T3 trim and lower case; T6 stored `anna@x.com`; T11 |
| F1.AC12 | T3, T6, T11 | T3 length 129; T6 no user created |
| F1.AC13 | T13, T15 | T13 and T15 redirect after logout |

## Test plan
- **Unit (xUnit, Core):** email and password rules (T3); `AuthService` with fake DAOs and `FakeTimeProvider`, including the 24-hour cap (T5); the log redactor (T8); the error middleware (T1).
- **Integration (WebApplicationFactory + Testcontainers PostgreSQL):** migrations and the unique index (T2); DAOs, rollback and the parallel-insert race (T4); all four endpoints and the cookie flags (T6); the protected-by-default probe (T7); log capture (T8); cleanup (T9); static fallback (T14). Each test class gets its own database.
- **Front end (Angular Testing Library):** guard, auth state and error mapping (T10); register (T11); log in (T12); placeholder and log out (T13).
- **E2E (Playwright):** the flows in T15.
- **Worked examples:** none. The product.md worked examples belong to F4–F6.
- **Manual checks:** phone-width layout of both forms; keyboard-only through register, log in and log out; a screen reader announcing field errors and the generic failure; 4.5:1 contrast on text and errors; visible focus on every control; reduced motion is not needed because there is no animation.

## Risks & mitigations
- **Session ID stored as-is.** The spec says the `id` is the cookie value, so a database leak would expose live sessions. The 24-hour cap limits this. Storing a hash of the ID instead is a small change if you want it, but it departs from the spec's wording.
- **No login rate limit (F1.R12).** An accepted risk in the spec. Password guessing is not slowed. The dummy-hash step keeps timing from leaking which emails exist.
- **Cross-site requests.** `SameSite=Lax` stops the cookie being sent on cross-site `POST` requests, and the API accepts JSON only. No separate anti-forgery token is added. Revisit if any state-changing `GET` is ever added.
- **Email check is only what the spec says.** `a@.` has one `@` and a dot after it, so it passes (F1.R13). Tightening this is a spec change, not a plan change.
- **Session cap is an addition to the spec.** The spec says a login lasts until the browser closes. The 24-hour server-side cap (your answer on 2026-10-21) means a browser left open for more than a day asks the user to log in again. The spec does not mention this. If you want it written there, run `/spec-feature F1`.
- **Mockup text that the spec's copy table does not list.** The regenerated mockups (2026-10-21) no longer show a show/hide icon in the password field, so nothing is left out there. They do show subtitles, the "Create account" and "Log in" button labels, the hero text and the "Your trips are private. Only you can see them." line. T11 and T12 use these as drawn, because the spec gives no other wording. Add them to the spec's States & errors table if you want them fixed in writing.
- **The F1 spec still names the old mockup files.** It points to `Log in.png` and `Register (error state).png`, which are now `F1 · Log in.png` and `F1 · Register (error state).png`. It also says the register mockup's "already registered" copy is not used, but the new mockup already shows the generic copy. The plan follows the new files. The spec needs a wording fix when you go through it, and I have not edited it.
- **Package licensing.** Pin FluentAssertions to a 7.x release. Version 8 and later use a commercial license. `Microsoft.Extensions.Identity.Core` (for `PasswordHasher<T>`) is the one new package beyond what CLAUDE.md names. Check this before T5.
- **Front-end test runner.** CLAUDE.md allows Jest or Karma/Jasmine. T10 uses whichever the Angular CLI generates. If it generates neither, stop and ask before choosing.
- **Open questions in the spec:** none.

## Out of scope
- Change password, password reset, email verification, other login providers, account deletion, and login attempt limits (spec, Out).
- The trip table, trip endpoints and trip screens, which are F2. The post-login placeholder (T13) is replaced there.
- Returning the user to the page they asked for after login (F1.R15).
- A CI or deployment pipeline. T14 only makes the app able to serve itself.

## Decisions log
- 2026-10-21: Which password hashing? → ASP.NET `PasswordHasher<T>` (PBKDF2), behind an `IPasswordHasher` interface in Core. No BCrypt (which also truncates input at 72 bytes, against the 128-character limit in F1.R14).
- 2026-10-21: Should the server cap a session's lifetime, given the cookie has no expiry? → Yes, an absolute cap of 24 hours, checked on every request and by an hourly cleanup job (config: `Auth:SessionMaxAge`, `Auth:SessionCleanupInterval`).
- 2026-10-21: _(Proposed in the plan, not asked; no product behavior changes.)_ Cookie `tet_session` with `HttpOnly`, `SameSite=Lax`, `Path=/`, `Secure` outside Development and no expiry. Paths `/api/auth/register`, `/login`, `/logout`, and `/me` (added so the front end can tell if it is logged in after a page reload). `401` ProblemDetails for a missing session. Validation returns field codes and the front end owns the copy. User ids are `uuid`. Passwords are measured in Unicode code points. Migrations run on startup only when `Database:MigrateOnStartup` is true.
- 2026-10-21: Mockups were regenerated and renamed with an `F1 · ` prefix. Revised T11, T12 and the Risks to follow them: new file names, the "Create account" and "Log in" button labels, the two subtitles, and no password show/hide icon. No task, ID or requirement coverage changed. The spec was not edited and its date is unchanged. Status set back to Draft until you re-approve.
