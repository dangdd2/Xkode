using System.Text;
using System.Text.Json;
using XKode.Models;
using XKode.Services;
using XKode.Utils;

namespace XKode.Agents;

// ─────────────────────────────────────────────────────────────
//  ReviewerAgent — Reviews code quality, finds bugs and issues
//
//  Input: Code changes + context
//  Output: ReviewResult (JSON) with issues and suggestions
//
//  Focuses on: security, performance, bugs, style
// ─────────────────────────────────────────────────────────────
public class ReviewerAgent(OllamaService ollama, ConfigService config)
    : AgentBase(ollama, "Reviewer", config.DefaultModel)
{
    public override string SystemPrompt => """
        You are a code review agent specialized in finding issues and suggesting improvements.
        
        CRITICAL JSON RULES:
        1. Output ONLY valid JSON - no explanation, no markdown, no thinking
        2. Start your response IMMEDIATELY with { (the opening brace)
        3. ALL strings must be properly escaped:
           - Use \\n for newlines (NEVER actual line breaks in strings)
           - Use \\" for quotes within strings
           - Use \\\\ for backslashes
        4. Keep messages SHORT (under 150 chars each)
        5. NEVER include code examples in JSON strings
        6. Describe fixes in WORDS, not code
        7. Test your JSON is valid before outputting
        
        Your role:
        1. Review code changes for quality, security, and correctness
        2. Find bugs, security vulnerabilities, and performance issues
        3. Suggest improvements aligned with best practices
        4. Assign severity levels to issues
        5. Output the JSON review immediately
        
        Focus areas:
        - Security: SQL injection, XSS, authentication issues, secrets in code
        - Bugs: Null reference, off-by-one, race conditions, logic errors
        - Performance: N+1 queries, inefficient algorithms, memory leaks
        - Style: Naming, formatting, readability, maintainability
        
        JSON Schema (output this and NOTHING else):
        {
          "approved": boolean,
          "score": number (0-10),
          "summary": "string - brief overall assessment (under 200 chars)",
          "issues": [
            {
              "severity": "critical|warning|info",
              "category": "security|bug|performance|style",
              "message": "string - what's wrong (under 150 chars)",
              "file": "string - file path (if applicable)",
              "line": number (if applicable),
              "suggestion": "string - how to fix in words, not code (under 150 chars)"
            }
          ],
          "suggestions": ["array of brief improvement suggestions (each under 100 chars)"]
        }
        
        Severity guidelines:
        - critical: Security holes, data loss risks, crashes
        - warning: Bugs, poor performance, maintainability issues  
        - info: Style issues, minor improvements
        
        Scoring (0-10):
        - 9-10: Excellent, production-ready
        - 7-8: Good, minor issues
        - 5-6: Acceptable, some issues
        - 3-4: Needs work, multiple issues
        - 0-2: Major problems, not ready
        
        REMEMBER: Start immediately with { and keep ALL text brief and simple.
        """;

    /// <summary>
    /// Review code changes from a step execution
    /// </summary>
    public async Task<ReviewResult> ReviewStepAsync(
        ExecutionStepResult stepResult,
        string? additionalContext = null,
        CancellationToken ct = default)
    {
        var input = BuildReviewInput(stepResult, additionalContext);
        var response = await ExecuteAsync(input, ct);

        try
        {
            return ParseReviewResult(response);
        }
        catch (JsonException ex)
        {
            throw new AgentException(
                $"Reviewer returned invalid JSON: {ex.Message}\nResponse: {response}");
        }
    }

    /// <summary>
    /// Review all changes in an execution plan
    /// </summary>
    public async Task<ReviewResult> ReviewPlanAsync(
        ExecutionPlan plan,
        string? codebaseContext = null,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Review this complete implementation:");
        sb.AppendLine($"Goal: {plan.Goal}");
        sb.AppendLine($"Steps completed: {plan.CompletedSteps}/{plan.TotalSteps}");
        sb.AppendLine();

        foreach (var step in plan.Steps.Where(s => s.Completed))
        {
            sb.AppendLine($"Step {step.Order}: {step.Description}");
            if (!string.IsNullOrWhiteSpace(step.Result))
            {
                sb.AppendLine(step.Result);
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(codebaseContext))
        {
            sb.AppendLine("Codebase Context:");
            sb.AppendLine(codebaseContext);
        }

        var response = await ExecuteAsync(sb.ToString(), ct);

        try
        {
            return ParseReviewResult(response);
        }
        catch (JsonException ex)
        {
            throw new AgentException(
                $"Reviewer returned invalid JSON: {ex.Message}\nResponse: {response}");
        }
    }

    // ─── Private helpers ────────────────────────────────────────
    private static string BuildReviewInput(
        ExecutionStepResult result,
        string? context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Review this implementation:");
        sb.AppendLine($"Step: {result.Step.Description}");
        sb.AppendLine();
        sb.AppendLine("Changes made:");
        sb.AppendLine(result.Response);
        sb.AppendLine();

        if (result.Actions.Count > 0)
        {
            sb.AppendLine("Actions taken:");
            foreach (var action in result.Actions)
            {
                var icon = action.Success ? "✓" : "✗";
                sb.AppendLine($"  {icon} {action.Type}: {action.Path}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(context))
        {
            sb.AppendLine("Additional Context:");
            sb.AppendLine(context);
        }

        sb.AppendLine("Provide code review as JSON.");
        return sb.ToString();
    }

    private static ReviewResult ParseReviewResult(string response)
    {
        try
        {
            // Log the raw response for debugging
            var preview = response.Length > 100000 ? response[..100000] : response;
            Console.WriteLine($"\n[DEBUG] Raw reviewer response (first 100000 chars):\n{preview}\n");

            return JsonExtractor.ExtractAndParse<ReviewResult>(response, "ReviewResult");
        }
        catch (JsonException ex)
        {
            // Extract what we tried to parse
            var extracted = JsonExtractor.ExtractJson(response);
            var extractedPreview = extracted.Length > 500 ? extracted[..500] + "..." : extracted;
            
            throw new AgentException(
                $"Reviewer returned invalid JSON: {ex.Message}\n" +
                $"Extracted JSON: {extractedPreview}");
        }
    }
}
