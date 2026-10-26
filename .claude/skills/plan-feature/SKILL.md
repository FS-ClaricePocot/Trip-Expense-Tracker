---
name: plan-feature
description: Write a step-by-step implementation plan for one Trip Expense Tracker feature (F<N>) from its Approved spec in docs/specs/features/, saved to docs/plans/ using the plan template. Refuses to plan from a Draft spec. Use when the user runs /plan-feature F<N>, or asks to plan, break down, or work out how to build a feature that has a spec, even if they don't say "plan" (for example "what are the tasks for F3?" or "how do we implement expenses?").
argument-hint: F<N> (e.g. F2)
model: sonnet
---

# Plan a feature

Turn one **Approved** spec from `/spec-feature` into an implementation plan at `docs/plans/F<N>-<slug>.md`, following [plan-template.md](plan-template.md). The plan is what a developer (or Claude) builds from, one task per commit, so it has to be concrete enough to start coding and complete enough that nothing in the spec gets missed.

Target feature: **$ARGUMENTS**

## 1. Find the spec and check it's approved (hard gate)

- Expect an ID like `F2` (accept `f2` or `2`). With no argument, list the specs in `docs/specs/features/` with their statuses and whether a plan exists, and ask which one to plan.
- Open `docs/specs/features/F<N>-*.md`. If none exists, stop and tell the user to run `/spec-feature F<N>` first.
- Read the status line (`_Status: … ·`). **If it isn't `Approved`, stop.** Say what the status is, and that the spec needs approving through `/spec-feature F<N>` first. A plan built on a draft gets thrown away when the spec changes, and changing a spec's status is the user's call, not yours.
- `<slug>` is the same as the spec filename's (`F4-expenses.md` → `docs/plans/F4-expenses.md`).

**Open questions in the spec.** List any unchecked `- [ ]` items and ask the user (AskUserQuestion) whether each one blocks planning. If one blocks, stop. If none block, plan around them: put each in the plan's Risks section, and make the task it affects say what is still open (for example "keyword list is a placeholder until F4 open question 1 is answered").

## 2. Gather context (read, don't skim)

- **The whole spec.** Its requirement IDs (`F<N>.R*`) and acceptance criterion IDs (`F<N>.AC*`) are what the plan must cover. Also read its Data & contract, States & errors, Accessibility and Edge cases sections: they turn into tasks and tests too.
- **`CLAUDE.md`**: the stack, the repo layout under `app/`, and the architecture rules. Tasks name real projects and folders from it. Don't introduce a framework or library it doesn't list without asking.
- **`docs/rules/project-rules.md`** and **`docs/specs/product.md`**: business rules R1–R8 and the worked examples. For F4–F6, the worked examples are required test cases.
- **Dependencies:** the specs and plans of features this one depends on (the spec's Dependencies section). For each, note whether it's built (code exists under `app/`), planned (`docs/plans/` has it), or not started. Reuse what an earlier plan already sets up (tables, DAOs, guards, error handling) instead of planning it twice.
- **Existing code** in `app/`. Match its patterns. If `app/` isn't scaffolded yet, the first tasks set up only the parts this feature needs, and say so.
- **Mockups** named in the spec's User flow section (`docs/design/mockups/`), for the UI tasks.
- **Existing plan:** if `docs/plans/F<N>-<slug>.md` already exists, revise it rather than starting over. Keep the IDs of tasks that haven't changed, give new tasks new numbers, and never reuse a removed task's ID. If the spec's "Last updated" date is newer than the one recorded in the plan's header, the spec changed after the plan was written: compare them and update every affected task.

## 3. Settle the decisions the spec hands to the plan

Specs often leave implementation choices to the plan on purpose ("decided in the plan", "the plan decides", "paths and exact shapes are decided in the plan"). Find every one of these and resolve it. For each:

- If `CLAUDE.md` or an earlier plan already settles it, apply that and cite it.
- If it's routine and follows directly from the conventions in `CLAUDE.md` (for example REST paths under `/api`, ProblemDetails errors, table and column names), propose it in the plan's Contract changes section without asking. The user reviews it when they review the plan.
- Otherwise, when the options have real trade-offs, ask the user with **AskUserQuestion** (at most 4 per call, most important first). Give 2–4 concrete options with their trade-offs, put your recommendation first, and add "(Recommended)" only when the stack, the rules, or common practice actually supports it.

Also ask when the plan needs a choice nothing settles: a library `CLAUDE.md` doesn't list, a config value (timeouts, cleanup intervals), or a build order that changes what users see first.

If a question is really about **product behavior** (what the user sees or what the rules are), the spec is incomplete. Don't decide it in the plan. Stop and send the user back to `/spec-feature F<N>`. Record every answer in the plan's Decisions log.

## 4. Write the plan

Fill in [plan-template.md](plan-template.md). How to shape the tasks:

- **IDs** are `F<N>.T1`, `F<N>.T2`, …, in the order they should be built, each building on finished work.
- **Order through the layers** the way `CLAUDE.md` lays them out: the DbUp migration first, then Core (models, pure logic, DAO interfaces), then Data (Dapper DAOs), then the API controllers, then the Angular feature folder, then E2E. Prefer thin vertical slices over finishing one layer at a time: for example "add expense" end to end before "delete expense", so something runs early.
- **Size:** each task is one reviewable commit. It names the files it creates or changes, what changes, which `R` and `AC` IDs it covers, the tests it adds, and a concrete **Done when** that someone else could check.
- **Human review:** mark a task **Needs human review** when it touches anything the project rules reserve for a person: authentication, secrets or environment files, database migrations, CI or deployment config, or deleting or disabling tests.
- **Rules that are easy to break:** call them out on the tasks where they apply. For example, money as `long` cents with the one parser in Core, ownership in the `WHERE` clause, multi-record writes in one transaction, parameterized SQL, the logger instead of console output, and nothing sensitive in logs.
- **Other features' code:** when a task has to change code another feature owns (for example F4 turning on F3's "in use" check), give it its own task, name the feature, and list it under Prerequisites, because that feature's tests can break.
- **Copy:** UI tasks use the exact copy from the spec's States & errors table. Don't rewrite it.
- **Describe, don't implement.** A short signature, SQL table sketch or request/response shape is fine when it pins down a contract. Full code is not: it goes stale and belongs in the commits.

**Coverage check before saving.** Every `F<N>.R*` maps to at least one task, every `F<N>.AC*` to at least one test, and each worked example the feature uses is a named test. Fill in the traceability table. If anything is uncovered, fix the plan before saving it, not after.

## 5. Finish

- Save the plan as `_Status: Draft`, with today's date and the spec's own "Last updated" date in the header.
- Reply with the file path, the number of tasks, confirmation that every requirement and acceptance criterion is covered, the tasks that need human review, any blocking or unbuilt dependencies, and the open risks.
- **Approval:** ask the user whether to approve the plan. Set `_Status: Approved` (with today's date) only when they explicitly say yes, in this run or later. Never approve a plan on your own: approval is the signal that coding can start.
