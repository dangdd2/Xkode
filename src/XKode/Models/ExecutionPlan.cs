using System.Text.Json.Serialization;

namespace XKode.Models;

// ─────────────────────────────────────────────────────────────
//  ExecutionPlan — Multi-step execution plan created by Planner
// ─────────────────────────────────────────────────────────────
public class ExecutionPlan
{
    [JsonPropertyName("goal")]
    public string Goal { get; set; } = "";

    [JsonPropertyName("context")]
    public string Context { get; set; } = "";

    [JsonPropertyName("steps")]
    public List<ExecutionStep> Steps { get; set; } = [];

    [JsonPropertyName("estimated_time")]
    public string EstimatedTime { get; set; } = "";

    [JsonPropertyName("complexity")]
    public string Complexity { get; set; } = "medium"; // low, medium, high

    public int TotalSteps => Steps.Count;
    public int CompletedSteps => Steps.Count(s => s.Completed);
    public bool IsComplete => Steps.All(s => s.Completed);
}

// ─────────────────────────────────────────────────────────────
//  ExecutionStep — Single step in the execution plan
// ─────────────────────────────────────────────────────────────
public class ExecutionStep
{
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "code"; // code, test, doc, config

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = [];

    [JsonPropertyName("dependencies")]
    public List<int> Dependencies { get; set; } = []; // Step orders this depends on

    [JsonPropertyName("estimated_minutes")]
    public int EstimatedMinutes { get; set; } = 5;

    // Runtime state
    public bool Completed { get; set; }
    public bool Skipped { get; set; }
    public string? Result { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration =>
        StartedAt.HasValue && CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : null;
}
