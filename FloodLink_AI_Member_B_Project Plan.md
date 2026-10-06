Backend — Inventory & Depot Management:
1. Entities: Depot (name, location) and InventoryItem (depot ID, item name, 
   quantity, unit). Simple CRUD, nothing elaborate.
2. API endpoints: list depots, list inventory per depot, add/update stock 
   quantity (the "check-in" action), get current stock levels.

Backend — Matching Agent:
3. Define/confirm AllocationProposal's real fields in FloodLink.Contracts. 
   Fix the AllocationProposalId placeholder issue.
4. Implement FloodLink.Agents.Matching: input TriagePlan, match against real 
   InventoryItem data from task 1, output AllocationProposal via 
   AgentResult<T>. No throwing for expected failures (no stock available).
5. Seed basic test data (a few depots, inventory items).
6. Tests: successful match, no-match case, partial-quantity case.
7. Wire into orchestrator, confirm Matching -> Routing / Matching -> Failed 
   transitions fire correctly.
8. Re-run the Triage -> Matching -> Routing -> Validating integration test 
   with the real agent.
9. Fix AllocationProposalId placeholder in RoutingAgent.cs now that real IDs 
   exist.

React — Inventory Dashboard:
10. A screen listing depots and their current stock levels (table view, 
    using Carbon DataTable per ui-context.md).
11. A simple form/action to update stock quantity for an item.

Flutter — Stock Check-in:
12. A screen where a user selects a depot and item, enters a quantity 
    received, and submits — calls the backend's stock-update endpoint.