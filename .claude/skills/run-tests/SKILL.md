---
name: run-tests
description: Run all unit tests for the Trip Expense Tracker and write the result to .reports/test-report.md (PASS or FAIL, the commit, counts per suite, failing test names). Use when the user runs /run-tests, asks to run the unit tests or check they pass, or before opening a pull request.
context: fork
model: haiku
---

# Run the unit tests

You run in your own context, so the long test output stays here. Run the tests, write the report file, and return only a short summary.

## 1. Note what you're testing

- `git rev-parse --short HEAD` → the commit.
- `git status --porcelain --untracked-files=no` → if it prints anything, there are uncommitted changes. The report then describes the working tree, not the commit, so say so in the report.

## 2. Run every suite

Take the commands from the **Commands** section of `CLAUDE.md`. Run each one from the repository root and keep only the summary lines and failing test names it describes. Filter the output with `grep` (and `head -n 30`); never print the full log.

- If a suite can't run at all (a build error, a locked file, a missing tool), record it as `ERROR` with the one line that explains why. `CLAUDE.md` names the known causes. Don't try to fix it: never edit code, tests or config, and never stop a running process.
- Use only the numbers you saw. Don't guess or round a count.

## 3. Write the report

Create `.reports/` if needed, then write `.reports/test-report.md` in exactly this layout:

```markdown
# Unit test report

Result: PASS
Commit: abc1234
Uncommitted changes: no
Run at: YYYY-MM-DD HH:MM UTC

| Suite | Result | Passed | Failed | Skipped | Total |
|---|---|---|---|---|---|
| Back end (xUnit) | PASS | 120 | 0 | 0 | 120 |
| Front end (Karma) | PASS | 182 | 0 | – | 182 |

## Failing tests
None.
```

- `Result: PASS` only when every suite is PASS. Any FAIL or ERROR makes it `Result: FAIL`.
- Under **Failing tests**, list each failing test as `- <suite>: <test name>`, and each ERROR suite with its reason.

The first lines are read by other steps (a person, and later the `/feature-loop` and `/open-pr` skills), so keep the field names exactly as shown.

## 4. Return

Reply with only: the `Result:` line, one line of counts per suite, and the failing tests (at most 10). Nothing else.
