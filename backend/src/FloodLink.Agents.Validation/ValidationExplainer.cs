using System.Text.Json.Nodes;
using FloodLink.Contracts;
using FloodLink.Domain.Llm;
using FloodLink.Infrastructure.Llm;

namespace FloodLink.Agents.Validation;

/// <summary>
/// Explains a plan's safety-check results in plain language for the coordinator (8.15). Display only:
/// pass/fail always comes from <see cref="SafetyRules"/>. The model's text is accepted only when it
/// names exactly the checks it was given and stays within length; otherwise a template explanation
/// built from the rule results is returned.
/// </summary>
public sealed class ValidationExplainer(ILlmClient llm)
{
    public const string PromptVersion = "validation-explainer.v1";
    public const int MaxSummaryLength = 300;
    public const int MaxExplanationLength = 240;
    private const int MaxDetailsPerCheck = 3;

    private static readonly Lazy<string> Prompt = new(() => PromptLibrary.Load(typeof(ValidationExplainer).Assembly, PromptVersion));

    private static readonly Dictionary<string, string> PassedText = new()
    {
        [SafetyRules.StockAvailability] = "Every depot has enough free stock for its part of the plan.",
        [SafetyRules.ReserveMinimumThreshold] = "Every depot keeps at least its reserve floor after the plan.",
        [SafetyRules.VehicleCapacity] = "Each truck's load fits its capacity.",
        [SafetyRules.CoordinateSanity] = "Depot and shelter positions are real and every route's distance and time are plausible.",
        [SafetyRules.RouteCompleteness] = "Every depot-to-shelter delivery in the plan has a route.",
    };

    /// <summary>The explanation and the LLM call record (prompt version, model, tokens, usedFallback).</summary>
    public async Task<(ValidationExplanation Explanation, ToolCall LlmCall)> ExplainAsync(ValidationResults results, CancellationToken ct = default)
    {
        var groups = results.Checks
            .GroupBy(c => c.CheckName)
            .Select(g => new
            {
                Name = g.Key,
                Passed = g.All(c => c.Passed),
                Total = g.Count(),
                Failed = g.Where(c => !c.Passed).ToList()
            })
            .ToList();

        var input = new JsonObject
        {
            ["overallPassed"] = results.OverallPassed,
            ["checks"] = new JsonArray(groups.Select(g => (JsonNode)new JsonObject
            {
                ["checkName"] = g.Name,
                ["passed"] = g.Passed,
                ["linesChecked"] = g.Total,
                ["linesFailed"] = g.Failed.Count
            }).ToArray())
        };
        var details = string.Join("\n", groups.SelectMany(g => g.Failed.Take(MaxDetailsPerCheck)
            .Select(c => $"{g.Name}: {c.ViolationDetail}")));

        var request = new LlmRequest(
            PromptVersion,
            Prompt.Value,
            $"<check_results>{input.ToJsonString()}</check_results>\n{LlmData.Fence("violation_details", details, 2000)}",
            ResponseSchema);
        var result = await llm.GenerateJsonAsync(request, ct);

        string? rejected = null;
        ValidationExplanation explanation;
        if (!result.Succeeded)
        {
            explanation = Template(groups.Select(g => (g.Name, g.Passed, g.Failed)).ToList(), results.OverallPassed,
                $"{result.ErrorCode}: {result.Error}");
        }
        else if (TryAccept(result.Json, groups.ToDictionary(g => g.Name, g => g.Passed), results.OverallPassed, out var accepted, out rejected))
        {
            explanation = accepted;
        }
        else
        {
            explanation = Template(groups.Select(g => (g.Name, g.Passed, g.Failed)).ToList(), results.OverallPassed,
                $"LLM_OUTPUT_REJECTED: {rejected}");
        }

        return (explanation, LlmToolCall.From(request, result, usedFallback: explanation.Source != "llm", rejected));
    }

    /// <summary>The deterministic check the model's explanation must pass before it is shown.</summary>
    public static bool TryAccept(JsonNode? json, IReadOnlyDictionary<string, bool> checks, bool overallPassed,
        out ValidationExplanation explanation, out string? reason)
    {
        explanation = null!;
        reason = null;
        var summary = json?["summary"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrEmpty(summary) || summary.Length > MaxSummaryLength)
        {
            reason = $"summary is empty or longer than {MaxSummaryLength} characters";
            return false;
        }
        if (json?["points"] is not JsonArray points)
        {
            reason = "missing points";
            return false;
        }

        var accepted = new List<ValidationExplanationPoint>();
        foreach (var point in points)
        {
            var name = point?["checkName"]?.GetValue<string>();
            var text = point?["explanation"]?.GetValue<string>()?.Trim();
            if (name is null || !checks.TryGetValue(name, out var passed))
            {
                reason = $"unknown check '{LlmData.CleanOutput(name, 40)}'";
                return false;
            }
            if (accepted.Any(p => p.CheckName == name))
            {
                reason = $"check {name} explained twice";
                return false;
            }
            if (string.IsNullOrEmpty(text) || text.Length > MaxExplanationLength)
            {
                reason = $"explanation of {name} is empty or longer than {MaxExplanationLength} characters";
                return false;
            }
            // Pass/fail comes from the rules; the model only supplies the words.
            accepted.Add(new ValidationExplanationPoint { CheckName = name, Passed = passed, Explanation = LlmData.CleanOutput(text, MaxExplanationLength) });
        }

        var missing = checks.Keys.Except(accepted.Select(p => p.CheckName)).ToList();
        if (missing.Count > 0)
        {
            reason = $"checks not explained: {string.Join(", ", missing)}";
            return false;
        }

        explanation = new ValidationExplanation
        {
            Source = "llm",
            PromptVersion = PromptVersion,
            Summary = LlmData.CleanOutput(summary, MaxSummaryLength),
            Points = checks.Keys.Select(k => accepted.Single(p => p.CheckName == k)).ToList(),
            OverallPassed = overallPassed
        };
        return true;
    }

    /// <summary>The explanation written from the rule results alone (no model).</summary>
    public static ValidationExplanation Template(
        IReadOnlyList<(string Name, bool Passed, List<ValidationCheck> Failed)> checks, bool overallPassed, string? reason)
    {
        var failedNames = checks.Where(c => !c.Passed).Select(c => c.Name).ToList();
        return new ValidationExplanation
        {
            Source = "template",
            PromptVersion = PromptVersion,
            FallbackReason = reason,
            OverallPassed = overallPassed,
            Summary = overallPassed
                ? $"All {checks.Count} safety checks passed, so the plan can be approved."
                : $"The plan was blocked by {string.Join(" and ", failedNames)}; it cannot be approved until that is fixed.",
            Points = checks.Select(c => new ValidationExplanationPoint
            {
                CheckName = c.Name,
                Passed = c.Passed,
                Explanation = c.Passed
                    ? PassedText.GetValueOrDefault(c.Name, "This check passed.")
                    : LlmData.CleanOutput(string.Join(" ", c.Failed.Take(MaxDetailsPerCheck).Select(f => f.ViolationDetail)), MaxExplanationLength)
            }).ToList()
        };
    }

    public static readonly JsonObject ResponseSchema = (JsonObject)JsonNode.Parse("""
    {
      "type": "OBJECT",
      "properties": {
        "summary": { "type": "STRING" },
        "points": {
          "type": "ARRAY",
          "items": {
            "type": "OBJECT",
            "properties": {
              "checkName": { "type": "STRING" },
              "explanation": { "type": "STRING" }
            },
            "required": ["checkName", "explanation"],
            "propertyOrdering": ["checkName", "explanation"]
          }
        }
      },
      "required": ["summary", "points"]
    }
    """)!;
}
