using System.Text;
using System.Text.Json;
using XKode.Models;
using XKode.Services;
using XKode.Utils;

namespace XKode.Agents;

// Extension methods for ExecutionPlan export/import
public static class ExecutionPlanExtensions
{
    /// <summary>
    /// Export plan to markdown format for user editing
    /// </summary>
    public static string ToMarkdown(this ExecutionPlan plan)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Execution Plan");
        sb.AppendLine();
        sb.AppendLine($"**Goal:** {plan.Goal}");
        sb.AppendLine($"**Complexity:** {plan.Complexity}");
        if (!string.IsNullOrWhiteSpace(plan.EstimatedTime))
            sb.AppendLine($"**Estimated Time:** {plan.EstimatedTime}");
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(plan.Context))
        {
            sb.AppendLine("## Context");
            sb.AppendLine(plan.Context);
            sb.AppendLine();
        }

        sb.AppendLine("## Steps");
        sb.AppendLine();

        foreach (var step in plan.Steps.OrderBy(s => s.Order))
        {
            sb.AppendLine($"### Step {step.Order}: {step.Description}");
            sb.AppendLine();
            sb.AppendLine($"- **Type:** {step.Type}");
            sb.AppendLine($"- **Estimated Time:** {step.EstimatedMinutes} minutes");
            
            if (step.Files.Count > 0)
            {
                sb.AppendLine($"- **Files:**");
                foreach (var file in step.Files)
                {
                    sb.AppendLine($"  - `{file}`");
                }
            }

            if (step.Dependencies.Count > 0)
            {
                sb.AppendLine($"- **Dependencies:** Steps {string.Join(", ", step.Dependencies)}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("- Edit steps as needed (add, remove, reorder)");
        sb.AppendLine("- Keep the markdown structure intact");
        sb.AppendLine("- Step numbers will be reassigned automatically");
        sb.AppendLine("- Save and run: `xkode agent --plan <this-file>`");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Import plan from markdown file
    /// </summary>
    public static ExecutionPlan FromMarkdown(string markdown)
    {
        var plan = new ExecutionPlan();
        var lines = markdown.Split('\n');
        
        ExecutionStep? currentStep = null;
        var stepNumber = 1;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Parse goal
            if (line.StartsWith("**Goal:**"))
            {
                plan.Goal = line.Replace("**Goal:**", "").Trim();
            }

            // Parse complexity
            else if (line.StartsWith("**Complexity:**"))
            {
                plan.Complexity = line.Replace("**Complexity:**", "").Trim();
            }

            // Parse estimated time
            else if (line.StartsWith("**Estimated Time:**"))
            {
                plan.EstimatedTime = line.Replace("**Estimated Time:**", "").Trim();
            }

            // Parse context section
            else if (line == "## Context")
            {
                var contextLines = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("##"))
                {
                    contextLines.Add(lines[i]);
                    i++;
                }
                plan.Context = string.Join("\n", contextLines).Trim();
                i--; // Back one line
            }

            // Parse step header
            else if (line.StartsWith("### Step"))
            {
                // Save previous step
                if (currentStep != null)
                {
                    plan.Steps.Add(currentStep);
                }

                // Extract description (everything after the colon)
                var colonIndex = line.IndexOf(':');
                var description = colonIndex > 0 
                    ? line[(colonIndex + 1)..].Trim() 
                    : "Step " + stepNumber;

                currentStep = new ExecutionStep
                {
                    Order = stepNumber++,
                    Description = description,
                    Type = "code",
                    EstimatedMinutes = 5
                };
            }

            // Parse step properties
            else if (currentStep != null)
            {
                if (line.StartsWith("- **Type:**"))
                {
                    currentStep.Type = line.Replace("- **Type:**", "").Trim();
                }
                else if (line.StartsWith("- **Estimated Time:**"))
                {
                    var timeStr = line.Replace("- **Estimated Time:**", "")
                        .Replace("minutes", "").Trim();
                    if (int.TryParse(timeStr, out var minutes))
                        currentStep.EstimatedMinutes = minutes;
                }
                else if (line.StartsWith("  - `") && line.EndsWith("`"))
                {
                    // File path
                    var file = line.Trim().TrimStart('-').Trim().Trim('`');
                    currentStep.Files.Add(file);
                }
                else if (line.StartsWith("- **Dependencies:**"))
                {
                    var depsStr = line.Replace("- **Dependencies:**", "")
                        .Replace("Steps", "").Trim();
                    var deps = depsStr.Split(',')
                        .Select(d => d.Trim())
                        .Where(d => int.TryParse(d, out _))
                        .Select(int.Parse)
                        .ToList();
                    currentStep.Dependencies = deps;
                }
            }
        }

        // Add last step
        if (currentStep != null)
        {
            plan.Steps.Add(currentStep);
        }

        // Validate
        if (string.IsNullOrWhiteSpace(plan.Goal))
            plan.Goal = "Imported plan";

        if (plan.Steps.Count == 0)
            throw new AgentException("No steps found in plan file");

        return plan;
    }
}
