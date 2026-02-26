using System.Text.Json;
using XKode.Models;
using XKode.Services;
using XKode.Utils;

namespace XKode.Agents;

// ─────────────────────────────────────────────────────────────
//  PlannerAgent — Breaks down complex tasks into ordered steps
//
//  Input: User request + codebase context
//  Output: ExecutionPlan (JSON) with ordered steps
//
//  Example:
//    Input: "Add authentication to my web app"
//    Output: {
//      "goal": "Add authentication...",
//      "steps": [
//        {"order": 1, "description": "Create User model", ...},
//        {"order": 2, "description": "Add JWT service", ...}
//      ]
//    }
// ─────────────────────────────────────────────────────────────
public class PlannerAgent(OllamaService ollama, ConfigService config)
    : AgentBase(ollama, "Planner", config.DefaultModel)
{
    public override string SystemPrompt => """
        You are a planning agent specialized in breaking down software development tasks.
        
        CRITICAL JSON RULES:
        1. Output ONLY valid JSON - no explanation, no markdown, no thinking
        2. Start your response IMMEDIATELY with { (the opening brace)
        3. ALL strings must be properly escaped:
           - Use \\n for newlines (NEVER actual line breaks in strings)
           - Use \\" for quotes within strings
           - Use \\\\ for backslashes
        4. Keep all text SHORT and simple (under 200 chars per field)
        5. Test your JSON is valid before outputting
        
        Your role:
        1. Analyze the user's request and codebase context
        2. Break the task into clear, ordered steps
        3. Identify files that need to be created or modified
        4. Estimate complexity and time
        5. Output the JSON plan immediately
        
        JSON Schema (output this and NOTHING else):
        {
          "goal": "string - what user wants to achieve",
          "context": "string - brief codebase summary",
          "complexity": "low|medium|high",
          "estimated_time": "string - e.g. '30 minutes'",
          "steps": [
            {
              "order": number,
              "description": "string - what to do",
              "type": "code|test|doc|config",
              "files": ["array of file paths"],
              "dependencies": [array of step orders this depends on],
              "estimated_minutes": number
            }
          ]
        }
        
        Guidelines:
        - Keep steps atomic (one clear action each)
        - Order steps logically (models before controllers, etc.)
        - Be specific about file paths
        - Include tests as separate steps
        - Maximum 100 steps for any task
        - If task is too large, break into phases
        
        REMEMBER: Start immediately with { and use ONLY simple text (no newlines in strings).
        """;

    /// <summary>
    /// Create an execution plan from user request
    /// </summary>
    public async Task<ExecutionPlan> CreatePlanAsync(
        string userRequest,
        string? codebaseContext = null,
        CancellationToken ct = default)
    {
        var input = BuildPlannerInput(userRequest, codebaseContext);
        var response = await ExecuteAsync(input, ct);

        // Parse JSON response
        try
        {
            var plan = ParsePlan(response);
            return plan;
        }
        catch (JsonException ex)
        {
            throw new AgentException(
                $"Planner returned invalid JSON: {ex.Message}\nResponse: {response}");
        }
    }

    // ─── Private helpers ────────────────────────────────────────
    private static string BuildPlannerInput(string request, string? context)
    {
        var input = $"Task: {request}\n\n";

        if (!string.IsNullOrWhiteSpace(context))
        {
            input += $"Codebase Context:\n{context}\n\n";
        }

        input += "Create an execution plan as JSON.";
        return input;
    }

    private static ExecutionPlan ParsePlan(string response)
    {
        try
        {
            var plan = JsonExtractor.ExtractAndParse<ExecutionPlan>(response, "ExecutionPlan");

            // Validate
             if (plan.Steps.Count == 0)
                throw new AgentException("Plan must have at least one step");

            if (plan.Steps.Count > 100)
                throw new AgentException("Plan has too many steps (max 10)");

            return plan;
        }
        catch (JsonException ex)
        {
            throw new AgentException($"Planner returned invalid JSON: {ex.Message}");
        }
    }
}

// ─────────────────────────────────────────────────────────────
//  AgentException — Thrown when agent fails
// ─────────────────────────────────────────────────────────────
public class AgentException(string message) : Exception(message);
