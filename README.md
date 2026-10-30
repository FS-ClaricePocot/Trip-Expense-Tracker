# Trip Expense Tracker

The sample app built in the L4 AI Developer Training Plan. A user registers, creates a trip, adds the people on it, and records who paid for what. The app shows what each person owes or is owed, and the fewest payments needed to settle up. See [docs/specs/product.md](docs/specs/product.md) for the full requirements.

## Prerequisites

- **[Claude Code](https://docs.claude.com/en/docs/claude-code)** installed and on your `PATH` (`claude --version`).
- **[Node.js](https://nodejs.org/)** (current LTS) on your `PATH` (`node --version`). The session-log hook runs with Node; without it no logs are written.
- **Windows:** [Git for Windows](https://git-scm.com/download/win) (includes Git Bash, which Claude Code uses to run hook commands).
- **[.NET SDK 10](https://dotnet.microsoft.com/download)** (current LTS) for the API (`dotnet --version`).
- **[Docker Desktop](https://www.docker.com/products/docker-desktop/)**, running with Linux containers, for the local database and the integration tests (`docker info`).

## Local setup

Run every command from the repository root, the folder that contains `app/` and `docs/`.

1. **Create your environment file.** Copy the example, then open `app/.env` and replace `POSTGRES_PASSWORD` with a password of your own. `app/.env` is git-ignored. Never commit it.

   ```bash
   cp app/.env.example app/.env
   ```

2. **Start the database.** This runs PostgreSQL 17 in Docker. The first run downloads the image. Data is kept in the `pgdata` volume between restarts.

   ```bash
   docker compose -f app/docker-compose.yml up -d
   ```

   Check that the `db` service shows `healthy`:

   ```bash
   docker compose -f app/docker-compose.yml ps
   ```

3. **Give the API its connection string.** It's stored with .NET user-secrets, in your user profile, outside the repo. Use the database name, user and password from your `app/.env`. The key must be exactly `ConnectionStrings:Default`.

   ```bash
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=trip_tracker_dev;Username=local_user;Password=<your app/.env password>" --project app/src/api/TripExpenseTracker.Api
   ```

   You don't need `dotnet user-secrets init`. The project's `UserSecretsId` is already committed. It's an ID, not a secret. To check the key name, run `dotnet user-secrets list --project app/src/api/TripExpenseTracker.Api` (this prints the value too, so don't share its output).

4. **Run the API.** In Development it applies the database migrations on startup. If it stops with "ConnectionStrings:Default is not set", step 3 didn't take: check the key name.

   ```bash
   dotnet run --project app/src/api/TripExpenseTracker.Api
   ```

5. **Run the tests.** The integration tests start their own throwaway PostgreSQL containers through Testcontainers, so they never touch your local database. Docker Desktop only needs to be running.

   ```bash
   dotnet test app/src/api/TripExpenseTracker.sln
   ```

What to know:

| Task | Command |
|---|---|
| Stop the database (data is kept) | `docker compose -f app/docker-compose.yml down` |
| Delete the local data and start fresh | `docker compose -f app/docker-compose.yml down -v` |
| Change the connection string | Run step 3 again with the new value |

- **Where configuration comes from:** user-secrets on your machine; each test's own container in tests; the `ConnectionStrings__Default` environment variable anywhere else. Don't put a connection string in `appsettings*.json`, since those files are committed.
- **Port 5432 already in use?** Change `POSTGRES_PORT` in `app/.env`, and use the same port in the connection string.

## Session logs

Every Claude Code session in this project is logged automatically to `docs/activities-outputs/session-logs/` by the hook in [.claude/hooks/session-log.mjs](.claude/hooks/session-log.mjs). It runs after every turn (`Stop`) and when the session ends (`SessionEnd`), as configured in [.claude/settings.json](.claude/settings.json). There is nothing to set up beyond the prerequisites.

| File | Contents |
|---|---|
| `YYYY-MM-DD-<session id>.md` | One per session: start and last activity time, prompt and task counts, models, token usage (main conversation and subagents), each task's tokens, and the skills and agents used. |
| `.hook-errors.log` | Only appears if the hook hit an error. Not committed (see `.gitignore`). |

What to know:

- **Your prompts are never recorded.** Each task is described from Claude's side: its plan steps if it made a plan, otherwise the opening sentence of its reply, otherwise the tools it used (e.g. `Tools: Bash ×2, Edit ×1`). A slash command is shown in front, e.g. `/init: …`. `Task N` appears only when there is nothing else to go on, such as a prompt interrupted before Claude did anything.
- **Logs cover your sessions only.** They are built from the transcripts on your machine.
- **Logs are committed** as evidence of each session's models and token use. Commit them with the work they belong to. Only `.hook-errors.log` stays out of git (see `.gitignore`).
- **Files are regenerated, don't edit them.** Each run rebuilds the logs from the session transcript.
- **Worktrees log separately.** A session in a Claude desktop-app worktree writes to that worktree's own `docs/activities-outputs/session-logs/`.
- **Dates use your local time zone.**

To log a session by hand:

```bash
node .claude/hooks/session-log.mjs --transcript <path-to-session.jsonl>
```

To exclude a session, add its full session id (one per line, `#` for comments) to `.claude/hooks/session-log.ignore`; its log is removed on the next run.

If logs stop appearing, check `docs/activities-outputs/session-logs/.hook-errors.log`, or run the hook with `SESSION_LOG_DEBUG=1` set to print errors.
