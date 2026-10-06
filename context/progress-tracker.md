# Progress Tracker

Update this file after every meaningful implementation
change.

## Current Phase

- Member B — Inventory, Matching Agent, and client screens — **COMPLETE**

## Current Goal

- Integrate Member B's inventory and matching work with the remaining team components.

## Completed

- Member C (orchestration, state machine, logger, Route/ETA agent via Mapbox, JWT validation)
  merged into Member A's branch; conflicts resolved, solution builds, 69 tests pass.
- Member B inventory scope completed: `Depot`, `InventoryItem`, and persisted
  `AllocationProposalEntity` tables; CRUD/check-in endpoints; seed stock; EF Core
  migration; greedy matching agent; and matching/orchestrator tests.
- `AllocationProposal` now carries a real persisted `AllocationProposalId`; the
  Route Agent uses database depot/shelter coordinates and this ID instead of
  placeholders.
- React Carbon inventory dashboard and Flutter Provider stock check-in screen added.

## In Progress

- None.

## Next Up

- Apply the new migration to the shared development database, then merge and
  integrate the remaining Validation/Approval work.

## Open Questions

- `/context` is in .gitignore (from main); existing files stay tracked but new files there are ignored.

## Architecture Decisions

- [Decisions made that affect the system design or
  data model — include why the decision was made]

## Session Notes

- Routes table already matches the agreed minimal schema (Id, WorkflowRunId, DistanceMeters,
  EstimatedDurationSeconds, PolylineString, CreatedAt); no migration needed. Build Spec 3.1 and
  Development Plan updated to match. Id and CreatedAt are kept deliberately (PK and audit timestamp).
- Merge: Program.cs now registers A's services/CORS plus C's JWT, Mapbox, orchestrator.
  Added AgentInvokerAdapters.cs (Api) bridging ITriageAgent/IMatchingAgent/IValidationAgent to the
  orchestrator invoker interfaces; Validation is still a stub (run -> Failed).
- Member B added an `IDesignTimeDbContextFactory`, so EF Core migrations use
  `ConnectionStrings__DefaultConnection` and no longer need the API host or JWT configuration.
