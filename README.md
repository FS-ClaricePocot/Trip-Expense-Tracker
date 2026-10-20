# Trip Expense Tracker

The sample app built in the L4 AI Developer Training Plan. A user registers, creates a trip, adds the people on it, and records who paid for what. The app shows what each person owes or is owed, and the fewest payments needed to settle up. See [docs/specs/product.md](docs/specs/product.md) for the full requirements.

## Prerequisites

- **[Claude Code](https://docs.claude.com/en/docs/claude-code)** installed and on your `PATH` (`claude --version`).
- **[Node.js](https://nodejs.org/)** (current LTS) on your `PATH` (`node --version`). The session-log hook runs with Node; without it no logs are written.
- **Windows:** [Git for Windows](https://git-scm.com/download/win) (includes Git Bash, which Claude Code uses to run hook commands).

## Session logs

Every Claude Code session in this project is logged automatically to `docs/activities-outputs/session-logs/` by the hook in [.claude/hooks/session-log.mjs](.claude/hooks/session-log.mjs). It runs after every turn (`Stop`) and when the session ends (`SessionEnd`), as configured in [.claude/settings.json](.claude/settings.json). There is nothing to set up beyond the prerequisites.

| File | Contents |
|---|---|
| `YYYY-MM-DD-<session id>.md` | One per session: start and last activity time, prompt and task counts, models, token usage (main conversation and subagents), each task's tokens, and the skills and agents used. |
| `.hook-errors.log` | Only appears if the hook hit an error. Not committed (see `.gitignore`). |

What to know:

- **Your prompts are never recorded.** Each task is described from Claude's side: its plan steps if it made a plan, otherwise the opening sentence of its reply, otherwise the tools it used (e.g. `Tools: Bash ×2, Edit ×1`). A slash command is shown in front, e.g. `/init: …`. `Task N` appears only when there is nothing else to go on, such as a prompt interrupted before Claude did anything.
- **Logs cover your sessions only.** They are built from the transcripts on your machine.
- **Logs are not committed.** `docs/activities-outputs/session-logs/` is in `.gitignore`, so the logs stay on your machine.
- **Files are regenerated, don't edit them.** Each run rebuilds the logs from the session transcript.
- **Worktrees log separately.** A session in a Claude desktop-app worktree writes to that worktree's own `docs/activities-outputs/session-logs/`.
- **Dates use your local time zone.**

To log a session by hand:

```bash
node .claude/hooks/session-log.mjs --transcript <path-to-session.jsonl>
```

To exclude a session, add its full session id (one per line, `#` for comments) to `.claude/hooks/session-log.ignore`; its log is removed on the next run.

If logs stop appearing, check `docs/activities-outputs/session-logs/.hook-errors.log`, or run the hook with `SESSION_LOG_DEBUG=1` set to print errors.
