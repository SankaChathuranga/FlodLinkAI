# Contributing to FloodLink AI

Thank you for contributing! Please read this document before opening a branch or PR.

---

## Folder Structure

```
FloodLink/
├── backend/   # ASP.NET Core API, EF Core, PostgreSQL, xUnit tests
├── web/       # Vite + React + TypeScript + Tailwind CSS (coordinator dashboard)
└── mobile/    # Flutter (field volunteer app)
```

> **Important:** All four members contribute across **all three folders** — there is no
> "one person per folder" division. Each member owns a vertical slice: their backend
> endpoints, their React screens, their Flutter screens, and their agent.

---

## Ownership

| Area | Owner |
|------|-------|
| Workflow state machine (`WorkflowRun`, transitions) | **Sanka (Member C)** |
| `FloodLink.Domain` entity and enum definitions | **Sanka (Member C)** |
| `FloodLink.Infrastructure` (AppDbContext, migrations) | **Sanka (Member C)** |
| `FloodLink.Agents.Routing` (Route/ETA Agent) | **Sanka (Member C)** |
| GitHub Actions CI (`.github/workflows/ci.yml`) | **Sanka (Member C)** |
| `FloodLink.Agents.Triage` + Shelters/Reports endpoints | **Sharani (Member A)** |
| `FloodLink.Agents.Matching` + Depots/Inventory endpoints | **Eshini (Member B)** |
| `FloodLink.Agents.Validation` + Dispatches/Audit endpoints | **Ijini (Member D)** |

---

## Rules

### 1. Shared Contracts (`FloodLink.Contracts`)

Any change to `FloodLink.Contracts` — adding, renaming, or removing a record field —
**requires a PR reviewed and approved by all four team members** before merge.
The contracts are the integration boundary between agents and must never be changed
unilaterally.

### 2. Agent Interface Contract

Every agent implements its own interface and must return `AgentResult<T>`:

```csharp
// ✅ Correct — use AgentResult for anticipated failures
public async Task<AgentResult<Route>> ExecuteAsync(AllocationProposal input)
{
    if (input is null)
        return AgentResult<Route>.Fail("INPUT_NULL", "Input must not be null.");
    // ... logic
}

// ❌ Wrong — do not throw for expected/anticipated failures
public async Task<AgentResult<Route>> ExecuteAsync(AllocationProposal input)
{
    if (input is null) throw new ArgumentException("null"); // Don't do this
}
```

Reserve `throw` / exceptions for truly unrecoverable/unexpected errors only.

### 3. React State Management

All shared application state in the web dashboard **must go through the React
Context API only**. Do not introduce Redux, Zustand, Jotai, Recoil, or any other
third-party state management library. This keeps the codebase scope-appropriate and
consistent for a 4-person team over 9 weeks.

### 4. Flutter State Management

All shared state in the mobile app **must use the `provider` package only**. Do not
introduce Riverpod, BLoC, GetX, or any other state management library. See
`mobile/lib/main.dart` for the established `ChangeNotifier + Provider` pattern.

---

## Branch Naming

```
feature/<member-initial>-<short-description>
```

Examples:
- `feature/sc-workflow-state-machine`
- `feature/sh-report-submission`
- `feature/es-inventory-reserve`
- `feature/ij-approval-flow`

**No direct commits to `main`.** All changes go through a PR.

---

## Pull Request Rules

1. **CI must pass** — all three jobs (backend, web, mobile) must be green before
   merge. Never merge a red CI.
2. **One approval required** — at least one teammate must review and approve before
   you merge.
3. **All four approvals required** for changes to `FloodLink.Contracts`.
4. Link the PR to a GitHub Issue describing the task.
5. Keep PRs focused: one feature or fix per PR, not a dump of multiple unrelated
   changes.

---

## Code Style

- C#: follow standard .NET conventions (`PascalCase` for types/methods,
  `camelCase` for locals). Use `async`/`await` throughout — no `.Result` or `.Wait()`.
- TypeScript/React: functional components + hooks only. No class components.
- Dart/Flutter: follow `flutter analyze` lint rules. Run `flutter analyze` before pushing.
