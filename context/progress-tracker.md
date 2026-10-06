# Progress Tracker

Update this file after every meaningful implementation
change.

## Current Phase

- [e.g. Not started / In progress / Complete]

## Current Goal

- [What you are building right now]

## Completed

- Member C (orchestration, state machine, logger, Route/ETA agent via Mapbox, JWT validation)
  merged into Member A's branch; conflicts resolved, solution builds, 69 tests pass.

## In Progress

- None yet.

## Next Up

- [First unit to build]

## Open Questions

- Routes table (DistanceMeters / EstimatedDurationSeconds / PolylineString, keyed by WorkflowRunId)
  differs from the shared spec (allocation_proposal_id, distance_km, eta_minutes, route_polyline).
  Needs team agreement or a mapping back to the contract.
- `/context` is in .gitignore (from main); existing files stay tracked but new files there are ignored.

## Architecture Decisions

- [Decisions made that affect the system design or
  data model — include why the decision was made]

## Session Notes

- Merge: Program.cs now registers A's services/CORS plus C's JWT, Mapbox, orchestrator.
  Added AgentInvokerAdapters.cs (Api) bridging ITriageAgent/IMatchingAgent/IValidationAgent to the
  orchestrator invoker interfaces; Matching and Validation are still stubs (run -> Failed).
- AppDbContextModelSnapshot.cs was regenerated from the merged model. `dotnet ef` cannot build a host
  from Program.cs (needs a design-time factory); API needs Jwt__SigningKey set to start.
