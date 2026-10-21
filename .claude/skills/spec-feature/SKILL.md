---
name: spec-feature
description: Write a detailed feature spec for one Trip Expense Tracker feature (F<N>) from docs/specs/product.md, asking the user about anything the product doc does not settle. Use when the user runs /spec-feature F<N> or asks to spec, detail, or flesh out a feature by its ID.
argument-hint: F<N> (e.g. F2)
---

# Spec a feature

Turn one feature row in `docs/specs/product.md` into a detailed, buildable spec at `docs/specs/features/F<N>-<slug>.md`.

Target feature: **$ARGUMENTS**

## 1. Resolve the feature

- Expect an ID like `F2` (accept `f2` or `2`). No argument → list the feature IDs and names from product.md and ask which one.
- Find its row in the tables (MVP). Not found → say so and list the valid IDs. Don't guess a close match.
- `<slug>` = the feature name in kebab-case (`F1-registration-and-login.md`).
- If that file already exists, read it and **revise** it: keep earlier answers in its Decisions log, and only ask about what's still open or has changed. If you change the substance of an `Approved` spec, set it back to `Draft` and note that in the Decisions log, because its plan (`docs/plans/`) may now be out of date.

## 2. Gather context (read, don't skim)

- All sections of `docs/specs/product.md`. A feature usually touches several of these sections.
- Other files in `docs/specs/features/`, to stay consistent and spot dependencies.

## 3. Separate what's known from what isn't

Before you write anything, sort every detail the spec needs into three groups:

- **Stated:** written in product.md or another spec. Use it, and cite the section.
- **Inferable:** follows directly from a stated rule. Use it and say what it's derived from.
- **Unknown:** anything else, such as product behavior, limits, copy, edge-case handling, or priorities. **Don't guess these.**

What counts as a guess: picking a number (limits, sizes, timeouts, counts), choosing between plausible behaviors, writing user-facing wording the user hasn't given, or deciding what's in or out of scope.

## 4. Ask the user

- Ask the unknowns with **AskUserQuestion**, up to 4 questions per call, the most important first. Make another call if there are more.
- Each question should be concrete and have 2–4 realistic options with their trade-offs. Mark your recommendation first and add "(Recommended)" only when the doc or best practice actually supports it.
- Don't ask what's already answered, and don't ask about implementation details the stack already decides.
- If the user skips a question or answers "not sure", don't fill it in yourself. Put it under **Open questions**.
- If an answer contradicts product.md, point out the conflict and ask which one wins.

## 5. Write the spec

Create `docs/specs/features/F<N>-<slug>.md` using the template below. Use stable, prefixed IDs so code, tests, and commits can refer to them: requirements are `F<N>.R1`, `F<N>.R2`, …, and acceptance criteria are `F<N>.AC1`, …. Leave out a section only if it truly doesn't apply, and say why in one line.

```markdown
# F<N> — <Feature name>

_Status: Draft · Last updated: <YYYY-MM-DD> · Source: product.md §3_

## Summary
What it does and why it matters to the user. 2–4 sentences.

## User stories
- As a <who>, I want <what>, so that <why>.

## Scope
**In:** …
**Out:** … (link the F-IDs that cover it, if any)

## User flow & screens
Step-by-step flow. Reference the mock screens on `docs/design` (if available) by screen name.

## Requirements
- **F<N>.R1** …

## Data & contract
Changes to schemas, browser storage, or the API (request and response, error shapes). Write "None" if there are none.

## States & errors
Loading, empty, validation errors, and the user-facing copy for each.

## Accessibility
Keyboard, focus, labels, contrast, and reduced motion, as they apply to this feature.

## Edge cases
- …

## Acceptance criteria
- **F<N>.AC1** Given … when … then …

## Dependencies
Other features (F-IDs), build-order step, and external services.

## Test plan
Unit tests, each mapped to the AC IDs.

## Open questions
- [ ] … (anything unanswered; never filled in with a guess)

## Decisions log
- <YYYY-MM-DD>: <question> → <user's answer>
```

## 6. Finish

- If the user's answers change something product.md states (a new limit, a scope change, a resolved §9 open question), **propose** the product.md edit and apply it only if the user agrees.
- Reply with the file path, the number of requirements and acceptance criteria, and the list of open questions still unanswered.
- **Approval:** a spec is saved as `Draft`. Ask the user whether to approve it. Set `_Status: Approved` (and today's date) only when the user explicitly says yes, in this run or later. Never approve a spec on your own. `/plan-feature` only runs on Approved specs.
