using System.Text;

namespace FloodLink.Domain.Llm;

/// <summary>
/// Puts untrusted text (report notes, coordinator objectives and revision notes) into a prompt as
/// data. The text is trimmed, length-limited, stripped of control characters and has angle
/// brackets escaped, then wrapped in a tag, so it cannot close its own fence or pose as instructions.
/// </summary>
public static class LlmData
{
    public static string Sanitize(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (var c in text.Trim())
        {
            if (sb.Length >= maxLength)
                break;
            if (char.IsControl(c))
                sb.Append(' ');
            else if (c == '<')
                sb.Append("&lt;");
            else if (c == '>')
                sb.Append("&gt;");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Wraps sanitized text in <c>&lt;tag attrs&gt;…&lt;/tag&gt;</c>.</summary>
    public static string Fence(string tag, string? text, int maxLength, string? attributes = null)
        => $"<{tag}{(attributes is null ? "" : " " + attributes)}>{Sanitize(text, maxLength)}</{tag}>";

    /// <summary>Cleans model-written text before it is stored (single line, length-limited).</summary>
    public static string CleanOutput(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var cleaned = new string(text.Trim().Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }
}
