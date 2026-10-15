using System.Reflection;

namespace FloodLink.Infrastructure.Llm;

/// <summary>
/// Reads a versioned prompt shipped as an embedded resource (<c>Prompts/&lt;name&gt;.v&lt;n&gt;.md</c> in an
/// agent project). The file name without extension (e.g. <c>triage.v1</c>) is the prompt version
/// logged with every LLM call, so a changed prompt must get a new file name.
/// </summary>
public static class PromptLibrary
{
    public static string Load(Assembly assembly, string version)
    {
        var suffix = $".Prompts.{version}.md";
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Prompt '{version}' is not embedded in {assembly.GetName().Name}.");
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
