# ADR-010 — Gemini-backed Planner, Triage and Matching; deterministic Route and Validation

- **Status:** Accepted (12 Oct 2026). Supersedes ADR-004 ("Deterministic C# agents and orchestrator,
  without an LLM or agent framework").
- **Tasks:** `FloodLink_Project_Finalization.md` 3.01–3.10.

## Context

ADR-004 kept every agent rule-based. The product needs agents that use judgement where rules can't:
reading a volunteer's free-text note ("two diabetic residents, insulin needed today"), explaining a
plan to the coordinator, and following a coordinator's revision note ("use depot 2, the road to
depot 1 is flooded"). At the same time, the pipeline moves real relief stock, so a model mistake,
an outage or a hostile note must never change stock, skip a safety check or bypass human approval.

## Decision

1. **One LLM abstraction.** `ILlmClient` (Domain) with `GeminiClient` (Infrastructure), calling the
   Gemini REST `generateContent` endpoint. Every call uses structured JSON output against a response
   schema, temperature 0, a 30 s per-attempt timeout, at most 2 retries (timeouts, network errors,
   5xx, 429 with `Retry-After`), and the key in the `x-goog-api-key` header. The model is set by
   `Gemini:Model` (default `gemini-3.8-flash`, thinking level `low`; Google closed `gemini-2.5-flash` to new keys); the key by `Gemini:ApiKey` / `Gemini__ApiKey`.
   Tests use a scripted `FakeLlmClient`; CI never calls Gemini.
2. **The LLM proposes, deterministic code decides.** Three steps use the LLM. Each output is checked
   in code, and any failed check (or failed call) means the agent's rule-based behaviour is used:

   | Step | LLM output | Deterministic check | Fallback |
   |---|---|---|---|
   | Planner | 4 steps: agent, purpose, rationale | exactly Triage, Matching, Routing, Validation in that order; rationale ≤ 200 chars | default plan |
   | Triage | per report: adjustment, justification | every input report exactly once, no unknown ids, adjustment an integer in −15..+15, justification ≤ 200 chars; final score clamped 0–100 | rule score + template justification |
   | Matching | allocations + unfulfillable needs | depot/item exist in the free-stock snapshot, quantity > 0, no stock line or need over-allocated, every short need listed | greedy matcher |

   The planner's plan is advisory: the orchestrator always runs the four agents in the fixed order.
3. **Tools are run by the agent, not the model.** The allow-listed, read-only tools
   (`get_shelter_context`, the inventory snapshot, the report summary) are called by the agent with
   ids taken from the database, and their results are put in the prompt as data. The model cannot
   request a tool call, so a hostile note cannot make it read or write anything.
4. **Untrusted text is data.** Report notes, the coordinator's objective and revision notes are
   length-limited, stripped of control characters, have `<`/`>` escaped and are wrapped in tags
   (`<report_notes>`, `<objective>`, `<revision_notes>`). The prompts say the tagged text is data and
   must not be followed as instructions.
5. **Minimal data leaves the system.** Prompts carry ids, need types, quantities, scores, shelter
   occupancy numbers and the notes. No names, phone numbers, emails, photos or GPS points (free-tier
   prompts may be used by Google).
6. **Observability without leaking.** Each LLM call is logged as a tool call (`llm.generate`) in
   `AgentExecutionLog.ToolCallsJson` with prompt version, model, latency, attempts, token counts,
   `usedFallback` and the rejection reason. Prompts, raw model output, reasoning and keys are never
   stored; only the checked structured output and the short justification are kept.
7. **Versioned prompts.** Prompts live in `backend/src/FloodLink.Agents.*/Prompts/<name>.v<n>.md` as
   embedded resources. The file name is the logged version, so changing a prompt means a new file.

### Why the Route agent stays deterministic (3.07)

Routing is a factual question (road distance and driving time between two coordinates) that the
Mapbox Directions API answers exactly. An LLM would add latency, cost and the risk of an invented
distance or ETA, with nothing to gain: there is no judgement to make. The agent keeps calling
Mapbox (as a tool) for every depot → shelter pair and fails safely when Mapbox is unavailable.

### Why the Validation agent stays rule-based (3.08)

Validation is the safety gate between the agents and the human approver. Its checks (stock not
over-allocated, reserve floors kept, vehicle capacity, coordinates and route completeness) must be
predictable, repeatable and auditable: the same plan must always get the same verdict, and a
prompt-injected note or a model error must not be able to talk a plan through. Rules give that; an
LLM cannot. An LLM may later *explain* the results to the coordinator (8.15), but pass/fail stays
rule-based.

## Consequences

- With no key, an outage or a free-tier rate limit, runs still complete on the rule-based paths; the
  trace shows `usedFallback: true` and why.
- Planner, Triage and Matching each add up to one Gemini call per run (up to 3 attempts each); a
  slow model can add up to about a minute to a run, which the background runner absorbs.
- The safety guarantees do not depend on the model: stock changes still happen only through the
  stock service after validation and coordinator approval.
