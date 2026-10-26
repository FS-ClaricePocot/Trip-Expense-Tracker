# End-to-end tests (Playwright)

The suite runs headless Chromium against a separate test stack:

- PostgreSQL in its own compose project `trip-e2e` (own container, volume `trip-e2e_pgdata`, database and port).
- The API on that database, with migrations run on start.
- The built Angular app, served by the API on the same origin.

It never reads `app/.env` and never uses the dev database (`trip_tracker_dev`, port 5432). The script refuses both.

## Environment variables

| Variable | Required | Default | Notes |
|---|---|---|---|
| `E2E_POSTGRES_USER` | yes | none | Test database user. |
| `E2E_POSTGRES_PASSWORD` | yes | none | Test database password. Avoid `;` in it (it is part of the connection string). |
| `E2E_POSTGRES_DB` | no | `trip_tracker_e2e` | Must not be `trip_tracker_dev`. |
| `E2E_POSTGRES_PORT` | no | `5433` | Must not be `5432`. |
| `E2E_APP_PORT` | no | `5100` | API and web app port. |
| `E2E_BASE_URL` | no | `http://localhost:5100` | Must match `E2E_APP_PORT`. |

Set the credentials in your shell only. Do not put them in a tracked file.

## Run

Prerequisites: Docker running, `npm ci` in `app/src/web`, `npm ci` in `app/e2e`, and `npx playwright install chromium` once.

```powershell
cd app/e2e
$env:E2E_POSTGRES_USER = "e2e_user"
$env:E2E_POSTGRES_PASSWORD = "<a password you choose>"
npm test
```

`npm test` starts the stack through `playwright.config.ts` (`scripts/start-stack.mjs`), waits for `/api/health`, runs the specs, and stops the API at the end.

To start the stack by hand (for example, to browse it), run `npm run stack` in `app/e2e` and stop it with Ctrl+C.

## Reset the test database

The password is set only when the volume is first created. If you change `E2E_POSTGRES_PASSWORD`, reset the test volume first. This removes only the `trip-e2e` project:

```powershell
docker compose -p trip-e2e -f ../docker-compose.yml down -v
```

Each test registers its own unique email, so repeated runs on the same test database do not conflict.
