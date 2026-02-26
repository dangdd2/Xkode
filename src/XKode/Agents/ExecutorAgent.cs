using System.Text;
using XKode.Models;
using XKode.Services;

namespace XKode.Agents;

// ─────────────────────────────────────────────────────────────
//  ExecutorAgent — Executes individual steps from execution plan
//
//  Input: ExecutionStep + codebase context
//  Output: Code changes using ```write:path syntax
//
//  Focused on implementation, not planning or reviewing
// ─────────────────────────────────────────────────────────────
public class ExecutorAgent(
    OllamaService ollama,
    ConfigService config,
    CodeIndexService codeIndex,
    FileService fileService)
    : AgentBase(ollama, "Executor", config.DefaultModel)
{
    public override string SystemPrompt => """
        You are an execution agent specialized in implementing code changes.
        
        Your role:
        1. Implement ONE specific step at a time
        2. Write clean, working code
        3. Follow the project's existing patterns and style
        4. Use ```write:path/to/file.ext syntax for file changes
        5. Be thorough but focused on the current step only
        
        File editing syntax:
        ```write:src/Models/User.cs
        [complete file content here]
        ```
        
        Guidelines:
        - Always show COMPLETE file content when editing
        - Follow existing code style and naming conventions
        - Add comments for complex logic
        - Import necessary namespaces
        - Handle errors appropriately
        - Write defensive code (null checks, validation)
        
        For shell commands, use:
        ```bash
        dotnet add package MyPackage
        ```
        
        Focus on implementation quality. Don't review, just execute.
        """;

    /// <summary>
    /// Execute a single step from the plan
    /// </summary>
    public async Task<ExecutionStepResult> ExecuteStepAsync(
        ExecutionStep step,
        string projectRoot,
        string? codebaseContext = null,
        bool autoApprove = false,
        CancellationToken ct = default)
    {
        step.StartedAt = DateTime.UtcNow;

        try
        {
            var input = BuildExecutorInput(step, projectRoot, codebaseContext);
            var response = await ExecuteAsync(input, ct);

            // Clean response to remove "Thinking..." blocks
            var cleanedResponse = CleanResponse(response);

            step.Result = cleanedResponse;
            step.Completed = true;
            step.CompletedAt = DateTime.UtcNow;

            // Parse and apply changes
            var actions = await codeIndex.ExecuteActionsAsync(
                cleanedResponse, projectRoot, autoApprove, ct);

            return new ExecutionStepResult
            {
                Step = step,
                Response = cleanedResponse,
                Actions = actions,
                Success = !actions.Any(a => !a.Success)
            };
        }
        catch (Exception ex)
        {
            step.CompletedAt = DateTime.UtcNow;
            return new ExecutionStepResult
            {
                Step = step,
                Success = false,
                Error = ex.Message
            };
        }
    }

    // ─── Private helpers ────────────────────────────────────────
    private string BuildExecutorInput(
        ExecutionStep step,
        string projectRoot,
        string? context)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Execute this step:");
        sb.AppendLine($"Step {step.Order}: {step.Description}");
        sb.AppendLine($"Type: {step.Type}");
        sb.AppendLine();

        if (step.Files.Count > 0)
        {
            sb.AppendLine("Files to modify/create:");
            foreach (var file in step.Files)
            {
                sb.AppendLine($"  - {file}");

                // Include existing file content if it exists
                var fullPath = Path.Combine(projectRoot, file);
                var existing = fileService.ReadFile(fullPath);
                if (existing != null)
                {
                    sb.AppendLine($"\nCurrent content of {file}:");
                    sb.AppendLine("```");
                    sb.AppendLine(existing);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(context))
        {
            sb.AppendLine("\nCodebase Context:");
            sb.AppendLine(context);
        }

        sb.AppendLine("\nImplement this step now using ```write:path syntax for file changes.");
        return sb.ToString();
    }

    // ─── Clean Response Helper ─────────────────────────────────
    private static string CleanResponse(string response)
    {
        // Remove "Thinking..." sections that some models add
        var lines = response.Split('\n');
        var cleanedLines = new List<string>();
        bool inThinkingBlock = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Detect start of thinking blocks
            if (trimmed.StartsWith("Thinking", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("...thinking", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Let me think", StringComparison.OrdinalIgnoreCase))
            {
                inThinkingBlock = true;
                continue;
            }
            
            // Detect end of thinking blocks
            if (trimmed.StartsWith("...done thinking", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Done thinking", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Now I will", StringComparison.OrdinalIgnoreCase))
            {
                inThinkingBlock = false;
                continue;
            }
            
            // Skip lines that are part of thinking blocks
            if (!inThinkingBlock)
            {
                cleanedLines.Add(line);
            }
        }
        
        return string.Join('\n', cleanedLines).Trim();
    }
}

// ─────────────────────────────────────────────────────────────
//  ExecutionStepResult — Result of executing a single step
// ─────────────────────────────────────────────────────────────
public class ExecutionStepResult
{
    public ExecutionStep Step { get; set; } = new();
    public string Response { get; set; } = "";
    public List<ActionResult> Actions { get; set; } = [];
    public bool Success { get; set; }
    public string? Error { get; set; }

    public int FilesChanged => Actions.Count(a => a.Type == "file_write" && a.Success);
    public int CommandsRun => Actions.Count(a => a.Type == "shell" && a.Success);
}
