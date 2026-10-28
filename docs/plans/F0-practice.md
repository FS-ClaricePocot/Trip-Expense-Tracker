# F0 — Practice: Implementation plan

_Status: Approved · Last updated: 2026-10-28 · Spec: none (practice plan for trying `/feature-loop`; never merged)_

## Overview
F0 is a throwaway plan for trying the `/feature-loop` skill end to end on two tiny tasks. It adds two pure functions to Core with unit tests. Nothing in the app uses them, and the branch is closed without merging.

## Prerequisites
- **Dependencies:** none.
- **Setup:** none. The tests need no database.

## Architecture touchpoints
| Area | Files / modules | Change |
|---|---|---|
| `TripExpenseTracker.Core` | `Practice/Greeting.cs`, `Practice/Calculator.cs` | Two pure static functions, no I/O |
| Tests | `TripExpenseTracker.Tests/Practice/` | Unit tests only (xUnit, FluentAssertions) |

## Acceptance criteria (practice)
- **F0.AC1** Given the name "Sam", when `Greeting.For` is called, then it returns "Hello, Sam!".
- **F0.AC2** Given 2 and 3, when `Calculator.Add` is called, then it returns 5.

## Tasks

### F0.T1 — Core: greeting
- **Files:** `TripExpenseTracker.Core/Practice/Greeting.cs`, `TripExpenseTracker.Tests/Practice/GreetingTests.cs`.
- **Change:** a static class `Greeting` in namespace `TripExpenseTracker.Core.Practice` with `public static string For(string name)` that returns `"Hello, " + name + "!"`.
- **Covers:** F0.AC1.
- **Tests:** unit: "F0.AC1" in the test name or summary; `For("Sam")` returns `"Hello, Sam!"`.
- **Done when:** the test passes.

### F0.T2 — Core: add
- **Files:** `TripExpenseTracker.Core/Practice/Calculator.cs`, `TripExpenseTracker.Tests/Practice/CalculatorTests.cs`.
- **Change:** a static class `Calculator` in namespace `TripExpenseTracker.Core.Practice` with `public static int Add(int a, int b)` that returns `a + b`.
- **Covers:** F0.AC2.
- **Tests:** unit: "F0.AC2" in the test name or summary; `Add(2, 3)` returns `5`, and `Add(-1, 1)` returns `0`.
- **Done when:** the tests pass.

## Test plan
- **Unit (Core):** `GreetingTests` (F0.AC1), `CalculatorTests` (F0.AC2). Run with `dotnet test app/src/api/TripExpenseTracker.Tests --nologo -v q`.

## Decisions log
- 2026-10-28: Practice plan for the Week 4 `/feature-loop` dry run. Approved for practice only; the branch is closed without merging.
