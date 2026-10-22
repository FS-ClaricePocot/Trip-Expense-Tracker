# F<N> — <Feature name>: Implementation plan

_Status: Draft · Last updated: <YYYY-MM-DD> · Spec: [F<N>-<slug>](../specs/features/F<N>-<slug>.md) (Approved, last updated <spec date>)_

## Overview
What will be built, in 2–4 sentences, and where it sits in the build order (F1 → F6).

## Prerequisites
- **Dependencies:** each F-ID this needs, marked built / planned / not started, and what this plan reuses from it. Call out anything that blocks.
- **Setup:** scaffolding, local services (for example PostgreSQL in Docker Compose), environment variables or config this feature needs first. Write "None" if there is none.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| Migration (DbUp) | `app/…` | … |
| `TripExpenseTracker.Core` | … | … |
| `TripExpenseTracker.Data` | … | … |
| `TripExpenseTracker.Api` | … | … |
| Angular (`app/src/web/…`) | … | … |
| Tests | … | … |

Rules from `CLAUDE.md` and `project-rules.md` that apply here, and where they're easy to break.

## Contract changes
- **Database:** tables, columns, keys, indexes and constraints (short SQL sketch).
- **API:** method, path, request and response shapes, status codes and ProblemDetails errors.
- **Browser storage:** cookies or storage, or "None".

## Tasks

### F<N>.T1 — <short title>
- **Files:** …
- **Change:** …
- **Covers:** F<N>.R…, F<N>.AC…
- **Tests:** …
- **Done when:** …
- _Needs human review: <reason>_ (only when it applies)

### F<N>.T2 — …

## Traceability
| Requirement | Tasks | Acceptance criteria | Tests |
|---|---|---|---|
| F<N>.R1 | F<N>.T… | F<N>.AC… | … |

## Test plan
Unit tests only (project rules).
- **Unit (xUnit, Core):** …
- **Unit (xUnit, Api):** controllers built directly with fake services and a fake `ICurrentUser`: …
- **Front end (Angular Testing Library, mocked HTTP):** …
- **Code review (not testable with unit tests):** migration SQL; DAO checklist (owner in every `WHERE`, ordering, error-code mappings, transactions, locks); cascades: …
- **Existing suites:** F1's integration and Playwright tests must still pass. Run them once at the end of the feature.
- **Worked examples:** which product.md examples are tests, and where.
- **Manual checks:** phone-width layout, keyboard only, screen reader, contrast, focus in dialogs.

## Risks & mitigations
- … (including non-blocking open questions carried over from the spec)

## Out of scope
What this plan doesn't do, with the F-IDs that cover it.

## Decisions log
- <YYYY-MM-DD>: <question> → <user's answer>
