using System.Text.Json;

namespace FloodLink.Domain;

/// <summary>
/// Reads and writes the keyed sections of <c>WorkflowRun.PlanJson</c>
/// (<c>triagePlan</c>, <c>allocationProposal</c>, <c>route</c>, <c>validationResults</c>,
/// <c>revision</c>). Each agent's output lives under its own key; nothing else is stored there.
/// </summary>
public static class WorkflowPlan
{
    public const string TriagePlanKey = "triagePlan";
    public const string AllocationProposalKey = "allocationProposal";
    public const string RouteKey = "route";
    public const string ValidationResultsKey = "validationResults";
    public const string RevisionKey = "revision";

    /// <summary>Returns <paramref name="planJson"/> with <paramref name="key"/> set to <paramref name="value"/>.</summary>
    public static string Merge<T>(string? planJson, string key, T value)
    {
        var sections = Parse(planJson);
        sections[key] = JsonSerializer.SerializeToElement(value);
        return JsonSerializer.Serialize(sections);
    }

    /// <summary>Returns <paramref name="planJson"/> without <paramref name="key"/>.</summary>
    public static string? Remove(string? planJson, string key)
    {
        if (planJson is null)
            return null;
        var sections = Parse(planJson);
        sections.Remove(key);
        return JsonSerializer.Serialize(sections);
    }

    /// <summary>Reads the section stored under <paramref name="key"/>, or null when it is absent.</summary>
    public static T? TryRead<T>(string? planJson, string key) where T : class
    {
        if (string.IsNullOrWhiteSpace(planJson))
            return null;
        return Parse(planJson).TryGetValue(key, out var element)
            ? element.Deserialize<T>()
            : null;
    }

    /// <summary>Reads the section stored under <paramref name="key"/>.</summary>
    /// <exception cref="InvalidOperationException">The plan or the section is missing.</exception>
    public static T Read<T>(string? planJson, string key) where T : class
        => TryRead<T>(planJson, key)
           ?? throw new InvalidOperationException($"PlanJson does not contain section '{key}'.");

    private static Dictionary<string, JsonElement> Parse(string? planJson)
        => string.IsNullOrWhiteSpace(planJson)
            ? new Dictionary<string, JsonElement>()
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(planJson) ?? new Dictionary<string, JsonElement>();
}
