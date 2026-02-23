using Spectre.Console;
using XKode.Models;
using XKode.Services;

namespace XKode.Agents;

// ─────────────────────────────────────────────────────────────
//  AgentOrchestrator — Coordinates multi-agent workflow
//
//  Flow:
//    1. Planner creates execution plan
//    2. User reviews and approves plan
//    3. Executor executes each step
//    4. Reviewer reviews after each step
//    5. Final review of all changes
//
//  This is the "brain" that manages the entire process
// ─────────────────────────────────────────────────────────────
public class AgentOrchestrator(
    PlannerAgent planner,
    ExecutorAgent executor,
    ReviewerAgent reviewer,
    FileService fileService,
    ConfigService config)
{
    private readonly List<ExecutionStepResult> _stepResults = [];
    private string _skillContent = ""; // SKILL.md instructions
    private bool _reviewEnabled = true; // Review enabled by default

    /// <summary>
    /// Set SKILL content to be included in all agent contexts
    /// </summary>
    public void SetSkillContent(string skillContent)
    {
        _skillContent = skillContent;
    }

    /// <summary>
    /// Disable code review (for --no-review flag)
    /// </summary>
    public void DisableReview()
    {
        _reviewEnabled = false;
    }

    /// <summary>
    /// Execute a complete multi-agent task
    /// </summary>
    public async Task<OrchestratorResult> ExecuteTaskAsync(
        string userRequest,
        string projectRoot,
        bool autoApprove = false,
        CancellationToken ct = default)
    {
        var result = new OrchestratorResult { UserRequest = userRequest };

        try
        {
            // Phase 1: Planning
            AnsiConsole.MarkupLine("\n[bold cyan]🤖 Phase 1: Planning...[/]");
            var context = BuildCodebaseContext(projectRoot);
            var plan = await planner.CreatePlanAsync(userRequest, context, ct);
            result.Plan = plan;

            return await ExecutePlanAsync(plan, projectRoot, autoApprove, ct, result);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Execute a pre-made execution plan
    /// </summary>
    public async Task<OrchestratorResult> ExecutePlanAsync(
        ExecutionPlan plan,
        string projectRoot,
        bool autoApprove = false,
        CancellationToken ct = default,
        OrchestratorResult? result = null)
    {
        result ??= new OrchestratorResult { UserRequest = plan.Goal, Plan = plan };

        try
        {
            var context = BuildCodebaseContext(projectRoot);

            DisplayPlan(plan);

            if (!autoApprove && !ConfirmPlan())
            {
                result.Cancelled = true;
                return result;
            }

            // Phase 2: Execution
            AnsiConsole.MarkupLine("\n[bold cyan]🤖 Phase 2: Executing steps...[/]");

            foreach (var step in plan.Steps)
            {
                AnsiConsole.MarkupLine($"\n[bold]Step {step.Order}/{plan.TotalSteps}:[/] {step.Description}");

                var stepResult = await executor.ExecuteStepAsync(step, projectRoot, context, autoApprove, ct);
                _stepResults.Add(stepResult);

                if (!stepResult.Success)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Step failed:[/] {stepResult.Error}");
                    result.Success = false;
                    return result;
                }

                // Show actions taken
                foreach (var action in stepResult.Actions)
                {
                    var icon = action.Success ? "[green]✓[/]" : "[red]✗[/]";
                    AnsiConsole.MarkupLine($"  {icon} {action.Type}: {action.Path}");
                }

                // Phase 3: Review after each step (if enabled)
                if (_reviewEnabled)
                {
                    try
                    {
                        var review = await reviewer.ReviewStepAsync(stepResult, context, ct);

                        if (review.HasCriticalIssues())
                        {
                            AnsiConsole.MarkupLine($"\n[red]⚠️  Critical issues found![/]");
                            DisplayReview(review);

                            if (!autoApprove && !ConfirmContinue())
                            {
                                result.Cancelled = true;
                                return result;
                            }
                        }
                        else if (review.Score < 7)
                        {
                            AnsiConsole.MarkupLine($"\n[yellow]💡 Review score: {review.Score}/10[/]");
                            if (review.Suggestions.Count > 0)
                            {
                                AnsiConsole.MarkupLine("[grey]Suggestions:[/]");
                                foreach (var suggestion in review.Suggestions.Take(3))
                                {
                                    AnsiConsole.MarkupLine($"  • {Markup.Escape(suggestion)}");
                                }
                            }
                        }
                    }
                    catch (AgentException ex)
                    {
                        // Review failed due to JSON parsing
                        AnsiConsole.MarkupLine($"[yellow]⚠️  Review skipped (JSON parse error)[/]");
                        AnsiConsole.MarkupLine($"[grey]The reviewer produced invalid JSON. Continuing without review.[/]");

                        // Optionally log to file for debugging
                        if (System.Diagnostics.Debugger.IsAttached)
                        {
                            Console.WriteLine($"[DEBUG] Review error: {ex.Message}");
                        }
                    }
                }
            }

            // Phase 4: Final review
            if (!autoApprove)
            {
                try
                {
                    AnsiConsole.MarkupLine("\n[bold cyan]🤖 Phase 3: Final review...[/]");
                    var finalReview = await reviewer.ReviewPlanAsync(plan, context, ct);
                    result.FinalReview = finalReview;
                    DisplayReview(finalReview);

                    result.Success = finalReview.Approved;
                }
                catch (AgentException ex)
                {
                    // Final review failed - assume success anyway
                    AnsiConsole.MarkupLine($"[yellow]⚠️  Final review skipped: {Markup.Escape(ex.Message)}[/]");
                    result.Success = true;
                }
            }
            else
            {
                result.Success = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    // ─── UI Display Methods ─────────────────────────────────────
    private static void DisplayPlan(ExecutionPlan plan)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title($"[bold cyan]Execution Plan[/] [grey]({plan.TotalSteps} steps)[/]");

        table.AddColumn("#");
        table.AddColumn("Description");
        table.AddColumn("Type");
        table.AddColumn("Files");

        foreach (var step in plan.Steps)
        {
            table.AddRow(
                step.Order.ToString(),
                step.Description,
                step.Type,
                string.Join(", ", step.Files.Take(3)) +
                    (step.Files.Count > 3 ? $" +{step.Files.Count - 3} more" : "")
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine($"\n[grey]Goal:[/] {Markup.Escape(plan.Goal)}");
        AnsiConsole.MarkupLine($"[grey]Complexity:[/] {plan.Complexity}");
        if (!string.IsNullOrWhiteSpace(plan.EstimatedTime))
            AnsiConsole.MarkupLine($"[grey]Estimated time:[/] {plan.EstimatedTime}");
    }

    private static void DisplayReview(ReviewResult review)
    {
        AnsiConsole.MarkupLine($"\n[bold]Review Score:[/] {review.Score}/10");

        if (review.Issues.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[bold]Issues Found:[/]");

            foreach (var issue in review.Issues)
            {
                var color = issue.Severity switch
                {
                    "critical" => "red",
                    "warning" => "yellow",
                    _ => "grey"
                };

                var icon = issue.Severity switch
                {
                    "critical" => "🔴",
                    "warning" => "⚠️ ",
                    _ => "💡"
                };

                AnsiConsole.MarkupLine($"  [{color}]{icon} {Markup.Escape(issue.Message)}[/]");

                if (!string.IsNullOrWhiteSpace(issue.File))
                    AnsiConsole.MarkupLine($"     [grey]in {issue.File}" +
                        (issue.Line.HasValue ? $" line {issue.Line}" : "") + "[/]");

                if (!string.IsNullOrWhiteSpace(issue.Suggestion))
                    AnsiConsole.MarkupLine($"     [cyan]→ {Markup.Escape(issue.Suggestion)}[/]");
            }
        }

        if (!string.IsNullOrWhiteSpace(review.Summary))
        {
            AnsiConsole.MarkupLine($"\n[grey]{Markup.Escape(review.Summary)}[/]");
        }
    }

    // ─── User Confirmation Methods ──────────────────────────────
    private static bool ConfirmPlan()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[bold]Proceed with this plan?[/]")
                .AddChoices("✅ Yes, execute plan", "❌ No, cancel"));

        return choice.StartsWith("✅");
    }

    private static bool ConfirmContinue()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[bold]Continue despite issues?[/]")
                .AddChoices("✅ Yes, continue", "❌ No, stop here"));

        return choice.StartsWith("✅");
    }

    // ─── Helper Methods ─────────────────────────────────────────
    private string BuildCodebaseContext(string projectRoot)
    {
        var ctx = fileService.IndexProject(projectRoot, config.MaxContextFiles);
        var codebaseContext = ctx.ToPromptContext(maxChars: 30_000); // Limit context size

        // Prepend SKILL content if available
        if (!string.IsNullOrWhiteSpace(_skillContent))
        {
            return _skillContent + "\n\n" + codebaseContext;
        }

        return codebaseContext;
    }
}

// ─────────────────────────────────────────────────────────────
//  OrchestratorResult — Final result of multi-agent execution
// ─────────────────────────────────────────────────────────────
public class OrchestratorResult
{
    public string UserRequest { get; set; } = "";
    public ExecutionPlan? Plan { get; set; }
    public ReviewResult? FinalReview { get; set; }
    public bool Success { get; set; }
    public bool Cancelled { get; set; }
    public string? Error { get; set; }
}