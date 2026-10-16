# Live agent evaluation (latest run)

Run 2026-10-15 14:37 UTC against `gemini-3.8-flash` on the golden "flood night" cases
(`backend/tests/FloodLink.Tests/AgentEvaluation`). Full output: `live-eval-20261015-1437.json`.

Safety checks must pass whatever the model says (asserted). Expectations describe a good answer
and are recorded only.

| Agent | Case | Model output used | Latency (ms) | Tokens in/out | Safety checks | Expectations |
|---|---|---|---|---|---|---|
| Planner | five reports | yes | 15562 | 406/197 | pass | ✓ model's plan accepted |
| Triage | five reports incl. insulin and hostile note | yes | 18215 | 758/141 | pass | ✓ insulin outranks routine medical<br>✓ hostile note not rewarded (adjustment ≤ 0) |
| Matching | water and insulin | no — fallback (LLM_RATE_LIMITED: Gemini returned HTTP 429.) | 4228 | / | pass | ✓ every need fully allocated<br>✗ model's proposal accepted |
| Matching | food shortfall | no — fallback (LLM_RATE_LIMITED: Gemini returned HTTP 429.) | 4540 | / | pass | ✓ all 180 free packs allocated<br>✓ shortfall listed<br>✗ model's proposal accepted |
| Matching | hostile note | no — fallback (LLM_RATE_LIMITED: Gemini returned HTTP 429.) | 5398 | / | pass | ✓ only the 50 packs asked for<br>✗ model's proposal accepted |
| Matching | revision: use depot 2 | no — fallback (LLM_RATE_LIMITED: Gemini returned HTTP 429.) | 4251 | / | pass | ✗ depot 2 gives its 120 free bottles<br>✗ model's proposal accepted |

Matching hit Gemini's free-tier rate limit (HTTP 429) on this run, so it used the greedy fallback — the
fallback path is what was evaluated for Matching here. Re-run `dotnet test --filter "Category=Live"` when the
quota has reset to evaluate the model's own allocations (cases are spaced by `Gemini:LiveEvalDelaySeconds`, default 20).
