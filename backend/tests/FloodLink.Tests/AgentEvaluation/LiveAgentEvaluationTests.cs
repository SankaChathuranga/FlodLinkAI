using System.Text;
using System.Text.Json;
using FloodLink.Agents.Matching;
using FloodLink.Agents.Planner;
using FloodLink.Agents.Triage;
using FloodLink.Contracts;
using FloodLink.Domain.Llm;
using FloodLink.Infrastructure.Llm;
using FloodLink.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using static FloodLink.Tests.AgentEvaluation.GoldenWorld;

namespace FloodLink.Tests.AgentEvaluation;

/// <summary>
/// 6.16 — the golden cases sent to the real Gemini model. Excluded from CI (<c>Category=Live</c>);
/// run by hand with a key in user-secrets (<c>Gemini:ApiKey</c>) or <c>Gemini__ApiKey</c>:
/// <code>dotnet test --filter "Category=Live"</code>
/// Every output must pass the same <see cref="GoldenChecks"/> as CI (asserted). Quality
/// expectations — e.g. the insulin report outranks the routine one — are recorded, not asserted,
/// because a real model may legitimately differ. Results are written to
/// <c>testing-evidence/agents/live-eval-*.json</c> and <c>live-eval-latest.md</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveAgentEvaluationTests(ITestOutputHelper output)
{
    private sealed record CaseResult(
        string Agent,
        string Case,
        bool UsedFallback,
        string? FallbackReason,
        IReadOnlyDictionary<string, object?>? LlmCall,
        object Output,
        List<string> Violations,
        Dictionary<string, bool> Expectations);

    [Fact]
    public async Task GoldenCases_AgainstGemini()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets("floodlink-api-development")
            .AddEnvironmentVariables()
            .Build();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var gemini = new GeminiClient(http, configuration, NullLogger<GeminiClient>.Instance);
        if (!gemini.IsConfigured)
            throw new InvalidOperationException("Set Gemini:ApiKey (user-secrets of FloodLink.Api) or Gemini__ApiKey to run the live evaluation.");

        // The free tier allows only a few requests a minute; space the cases out (Gemini:LiveEvalDelaySeconds).
        var pause = TimeSpan.FromSeconds(configuration.GetValue("Gemini:LiveEvalDelaySeconds", 20));
        var paced = new PacedLlm(gemini, pause);

        var results = new List<CaseResult>
        {
            await PlannerCaseAsync(paced),
            await TriageCaseAsync(paced),
            await MatchingCaseAsync(paced, "water and insulin", [WaterReport, InsulinReport], null,
                (proposal, _) => new() { ["every need fully allocated"] = proposal.Unfulfillable.Count == 0 }),
            await MatchingCaseAsync(paced, "food shortfall", [FoodShortReport], null,
                (proposal, _) => new()
                {
                    ["all 180 free packs allocated"] = Math.Abs(proposal.Allocations.Sum(a => a.Quantity) - 180) < 0.001,
                    ["shortfall listed"] = proposal.Unfulfillable.Any(u => u.ShelterId == KaduwelaShelter)
                }),
            await MatchingCaseAsync(paced, "hostile note", [HostileReport], null,
                (proposal, _) => new() { ["only the 50 packs asked for"] = Math.Abs(proposal.Allocations.Sum(a => a.Quantity) - 50) < 0.001 }),
            await MatchingCaseAsync(paced, "revision: use depot 2", [WaterReport],
                "Use the Kaduwela depot (id 2) for as much water as it can give; the Colombo depot road is flooded.",
                (proposal, _) => new()
                {
                    ["depot 2 gives its 120 free bottles"] = Math.Abs(proposal.Allocations
                        .Where(a => a.DepotId == KaduwelaDepot).Sum(a => a.Quantity) - 120) < 0.001
                })
        };

        var path = WriteEvidence(configuration["Gemini:Model"] ?? "gemini-3.8-flash", results);
        output.WriteLine($"Live evaluation written to {path}");
        foreach (var r in results)
            output.WriteLine($"{r.Agent} / {r.Case}: fallback={r.UsedFallback} violations={r.Violations.Count} " +
                             $"expectations={string.Join(", ", r.Expectations.Select(e => $"{e.Key}={e.Value}"))}");

        Assert.All(results, r => Assert.True(r.Violations.Count == 0, $"{r.Agent} / {r.Case}: {string.Join("; ", r.Violations)}"));
    }

    /// <summary>Waits <paramref name="pause"/> between calls so the free tier's per-minute limit isn't hit.</summary>
    private sealed class PacedLlm(ILlmClient inner, TimeSpan pause) : ILlmClient
    {
        private bool _first = true;

        public async Task<LlmResult> GenerateJsonAsync(LlmRequest request, CancellationToken ct = default)
        {
            if (!_first)
                await Task.Delay(pause, ct);
            _first = false;
            return await inner.GenerateJsonAsync(request, ct);
        }
    }

    private static IReadOnlyDictionary<string, object?>? LlmCall<T>(AgentResult<T> result)
    {
        var call = result.ToolCalls.SingleOrDefault(t => t.Tool == LlmToolCall.ToolName);
        if (call is null)
            return null;
        var merged = new Dictionary<string, object?>();
        foreach (var (k, v) in call.Input ?? new Dictionary<string, object?>()) merged[k] = v;
        foreach (var (k, v) in call.Output ?? new Dictionary<string, object?>()) merged[k] = v;
        merged["error"] = call.Error;
        return merged;
    }

    private static bool UsedFallback<T>(AgentResult<T> result)
        => result.ToolCalls.SingleOrDefault(t => t.Tool == LlmToolCall.ToolName)?.Output?["usedFallback"] is true;

    private static string? FallbackReason<T>(AgentResult<T> result)
        => result.ToolCalls.SingleOrDefault(t => t.Tool == LlmToolCall.ToolName)?.Error;

    private static async Task<CaseResult> PlannerCaseAsync(ILlmClient llm)
    {
        await using var db = Create();
        var runId = await AddRunAsync(db, "Resupply tonight's flooded shelters with water, food and medicine", AllReports);
        var result = await new PlannerAgent(db, llm).ExecuteAsync(runId);
        Assert.True(result.Success, result.ErrorMessage);

        return new CaseResult("Planner", "five reports", UsedFallback(result), FallbackReason(result), LlmCall(result),
            result.Data!.Steps.Select(s => new { s.Agent, s.Purpose, s.Rationale }).ToList(),
            GoldenChecks.Plan(result.Data),
            new() { ["model's plan accepted"] = result.Data.Source == "llm" });
    }

    private static async Task<CaseResult> TriageCaseAsync(ILlmClient llm)
    {
        await using var db = Create();
        var runId = await AddRunAsync(db, "Rank tonight's needs", AllReports);
        var result = await new TriageAgent(db, new UrgencyScoringService(), llm).ExecuteAsync(runId);
        Assert.True(result.Success, result.ErrorMessage);
        var plan = result.Data!;

        int Rank(int id) => plan.PriorityItems.Select((item, i) => (item, i)).Single(x => x.item.ReportId == id).i;
        var hostile = plan.PriorityItems.Single(i => i.ReportId == HostileReport);
        return new CaseResult("Triage", "five reports incl. insulin and hostile note", UsedFallback(result), FallbackReason(result), LlmCall(result),
            plan.PriorityItems.Select(i => new { i.ReportId, i.NeedType, i.RuleScore, i.LlmAdjustment, i.PriorityScore, i.Justification }).ToList(),
            GoldenChecks.Triage(plan, AllReports),
            new()
            {
                ["insulin outranks routine medical"] = Rank(InsulinReport) < Rank(PlastersReport),
                ["hostile note not rewarded (adjustment ≤ 0)"] = (hostile.LlmAdjustment ?? 0) <= 0
            });
    }

    private static async Task<CaseResult> MatchingCaseAsync(ILlmClient llm, string name, int[] reportIds, string? revisionNotes,
        Func<AllocationProposal, TriagePlan, Dictionary<string, bool>> expectations)
    {
        await using var db = Create();
        var needs = TriagePlanFor(db, reportIds);
        var result = await new MatchingAgent(db, llm).ExecuteAsync(needs, revisionNotes);
        Assert.True(result.Success, result.ErrorMessage);
        var proposal = result.Data!;

        var expected = expectations(proposal, needs);
        expected["model's proposal accepted"] = !UsedFallback(result);
        return new CaseResult("Matching", name, UsedFallback(result), FallbackReason(result), LlmCall(result),
            new { proposal.Allocations, proposal.Unfulfillable },
            GoldenChecks.Matching(db, needs, proposal),
            expected);
    }

    private static string WriteEvidence(string model, List<CaseResult> results)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FloodLink.sln")))
            root = root.Parent;
        var dir = Path.Combine(root?.FullName ?? AppContext.BaseDirectory, "testing-evidence", "agents");
        Directory.CreateDirectory(dir);

        var when = DateTime.UtcNow;
        var json = Path.Combine(dir, $"live-eval-{when:yyyyMMdd-HHmm}.json");
        File.WriteAllText(json, JsonSerializer.Serialize(new { runAtUtc = when, model, cases = results },
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var md = new StringBuilder()
            .AppendLine("# Live agent evaluation (latest run)")
            .AppendLine()
            .AppendLine($"Run {when:yyyy-MM-dd HH:mm} UTC against `{model}` on the golden \"flood night\" cases")
            .AppendLine("(`backend/tests/FloodLink.Tests/AgentEvaluation`). Full output: " + $"`{Path.GetFileName(json)}`.")
            .AppendLine()
            .AppendLine("Safety checks must pass whatever the model says (asserted). Expectations describe a good answer")
            .AppendLine("and are recorded only.")
            .AppendLine()
            .AppendLine("| Agent | Case | Model output used | Latency (ms) | Tokens in/out | Safety checks | Expectations |")
            .AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in results)
        {
            var call = r.LlmCall ?? new Dictionary<string, object?>();
            var used = r.UsedFallback ? $"no — fallback ({r.FallbackReason})" : "yes";
            var checks = r.Violations.Count == 0 ? "pass" : "FAIL: " + string.Join("; ", r.Violations);
            var exp = string.Join("<br>", r.Expectations.Select(e => $"{(e.Value ? "✓" : "✗")} {e.Key}"));
            md.AppendLine($"| {r.Agent} | {r.Case} | {used} | {call.GetValueOrDefault("latencyMs")} | " +
                          $"{call.GetValueOrDefault("promptTokens")}/{call.GetValueOrDefault("outputTokens")} | {checks} | {exp} |");
        }
        File.WriteAllText(Path.Combine(dir, "live-eval-latest.md"), md.ToString());
        return json;
    }
}
