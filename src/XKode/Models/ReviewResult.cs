using System.Text.Json.Serialization;

namespace XKode.Models;

// ─────────────────────────────────────────────────────────────
//  ReviewResult — Output from ReviewerAgent after code review
// ─────────────────────────────────────────────────────────────
public class ReviewResult
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; } // 0-10

    [JsonPropertyName("issues")]
    public List<ReviewIssue> Issues { get; set; } = [];

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    // Computed properties
    public bool HasCriticalIssues() => Issues.Any(i => i.Severity == "critical");
    public bool HasWarnings() => Issues.Any(i => i.Severity == "warning");
    public int CriticalCount => Issues.Count(i => i.Severity == "critical");
    public int WarningCount => Issues.Count(i => i.Severity == "warning");
    public int InfoCount => Issues.Count(i => i.Severity == "info");
}

// ─────────────────────────────────────────────────────────────
//  ReviewIssue — Single issue found during code review
// ─────────────────────────────────────────────────────────────
public class ReviewIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "info"; // critical, warning, info

    [JsonPropertyName("category")]
    public string Category { get; set; } = ""; // security, performance, style, bug

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    //[JsonPropertyName("code_example")]
    //public string? CodeExample { get; set; }
}