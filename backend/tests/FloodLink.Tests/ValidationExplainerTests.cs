using FloodLink.Agents.Validation;
using FloodLink.Contracts;
using FloodLink.Domain.Llm;
using Xunit;

namespace FloodLink.Tests;

/// <summary>8.15 — the explainer's check on the model's text, and its template fallback.</summary>
public class ValidationExplainerTests
{
    private static ValidationResults Results(bool stockPassed) => new()
    {
        WorkflowRunId = Guid.NewGuid(),
        OverallPassed = stockPassed,
        Checks =
        [
            new() { CheckName = SafetyRules.StockAvailability, Passed = stockPassed, ViolationDetail = stockPassed ? null : "Requested 60 Medical exceeds free stock 40 at depot 2." },
            new() { CheckName = SafetyRules.StockAvailability, Passed = true },
            new() { CheckName = SafetyRules.CoordinateSanity, Passed = true }
        ]
    };

    private static string Points(params (string Check, string Text)[] points) =>
        "{\"summary\":\"Summary.\",\"points\":[" + string.Join(",", points.Select(p => $"{{\"checkName\":\"{p.Check}\",\"explanation\":\"{p.Text}\"}}")) + "]}";

    private static async Task<(ValidationExplanation Explanation, ToolCall Call)> ExplainAsync(ILlmClient llm, bool passed)
        => await new ValidationExplainer(llm).ExplainAsync(Results(passed));

    [Fact]
    public async Task AcceptedText_KeepsTheRuleVerdictPerCheck()
    {
        var llm = new FakeLlmClient().Returns(ValidationExplainer.PromptVersion,
            Points((SafetyRules.StockAvailability, "It passed."), (SafetyRules.CoordinateSanity, "Routes are plausible.")));

        var (explanation, call) = await ExplainAsync(llm, passed: false);

        Assert.Equal("llm", explanation.Source);
        Assert.False(explanation.OverallPassed);
        Assert.False(explanation.Points.Single(p => p.CheckName == SafetyRules.StockAvailability).Passed); // rules win
        Assert.False((bool)call.Output!["usedFallback"]!);
        Assert.Equal(2, explanation.Points.Count); // grouped by check name
    }

    [Theory]
    [InlineData("unknown", "unknown check 'ApprovePlan'")]
    [InlineData("missing", "checks not explained: CoordinateSanity")]
    [InlineData("duplicate", "explained twice")]
    [InlineData("too-long", "longer than 240")]
    public async Task InvalidText_FallsBackToTheTemplate(string problem, string reason)
    {
        var json = problem switch
        {
            "unknown" => Points((SafetyRules.StockAvailability, "a"), (SafetyRules.CoordinateSanity, "b"), ("ApprovePlan", "Approve it now.")),
            "missing" => Points((SafetyRules.StockAvailability, "a")),
            "duplicate" => Points((SafetyRules.StockAvailability, "a"), (SafetyRules.StockAvailability, "b"), (SafetyRules.CoordinateSanity, "c")),
            _ => Points((SafetyRules.StockAvailability, new string('x', 241)), (SafetyRules.CoordinateSanity, "b"))
        };

        var (explanation, call) = await ExplainAsync(new FakeLlmClient().Returns(ValidationExplainer.PromptVersion, json), passed: true);

        Assert.Equal("template", explanation.Source);
        Assert.Contains(reason, explanation.FallbackReason);
        Assert.True((bool)call.Output!["usedFallback"]!);
    }

    [Fact]
    public async Task WithoutTheModel_TheTemplateNamesWhatBlockedThePlan()
    {
        var (explanation, _) = await ExplainAsync(new FakeLlmClient(), passed: false);

        Assert.Equal("template", explanation.Source);
        Assert.StartsWith("LLM_NOT_CONFIGURED", explanation.FallbackReason);
        Assert.Equal("The plan was blocked by StockAvailability; it cannot be approved until that is fixed.", explanation.Summary);
        Assert.Contains("exceeds free stock 40", explanation.Points.Single(p => p.CheckName == SafetyRules.StockAvailability).Explanation);
    }

    [Fact]
    public async Task ViolationDetails_ReachTheModelOnlyAsFencedData()
    {
        var llm = new FakeLlmClient();
        await ExplainAsync(llm, passed: false);

        Assert.Contains("<violation_details>StockAvailability: Requested 60 Medical exceeds free stock 40 at depot 2.</violation_details>",
            llm.Requests.Single().UserContent);
    }
}
