using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using XKode.Agents;
using XKode.Models;
using XKode.Services;

namespace XKode.Commands;

// ─────────────────────────────────────────────────────────────
//  AgentCommand — Multi-agent execution mode
//
//  Usage:
//    xkode agent "Add authentication to my app"
//    xkode agent "Refactor Services folder" --path ./src
//    xkode agent "Write tests for UserController" --yes
//
//  Coordinates Planner → Executor → Reviewer agents
// ─────────────────────────────────────────────────────────────
public class AgentCommand(
    OllamaService ollama,
    ConfigService config,
    FileService fileService,
    CodeIndexService codeIndex,
    MarkdownService markdownService) : AsyncCommand<AgentCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[task]")]
        [Description("Task to execute using multi-agent workflow (optional if --plan is used)")]
        public string? Task { get; set; }

        [CommandOption("--plan <FILE>")]
        [Description("Load execution plan from markdown file")]
        public string? PlanFile { get; set; }

        [CommandOption("--export-plan <FILE>")]
        [Description("Export plan to markdown file and exit (don't execute)")]
        public string? ExportPlanFile { get; set; }

        [CommandOption("-p|--path")]
        [Description("Project root path (default: current directory)")]
        [DefaultValue(".")]
        public string Path { get; set; } = ".";

        [CommandOption("-m|--model")]
        [Description("Ollama model to use (default from config)")]
        public string? Model { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Auto-approve all steps (dangerous!)")]
        public bool AutoApprove { get; set; } = false;

        [CommandOption("--no-review")]
        [Description("Skip code review steps")]
        public bool NoReview { get; set; } = false;

        [CommandOption("--planner-model")]
        [Description("Specific model for planner agent")]
        public string? PlannerModel { get; set; }

        [CommandOption("--executor-model")]
        [Description("Specific model for executor agent")]
        public string? ExecutorModel { get; set; }

        [CommandOption("--reviewer-model")]
        [Description("Specific model for reviewer agent")]
        public string? ReviewerModel { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        Banner.Show();

        // Validate: need either task or plan file
        if (string.IsNullOrWhiteSpace(settings.Task) && string.IsNullOrWhiteSpace(settings.PlanFile))
        {
            AnsiConsole.MarkupLine("[red]Error: Either <task> or --plan is required[/]");
            AnsiConsole.MarkupLine("[grey]Usage:[/]");
            AnsiConsole.MarkupLine("  xkode agent \"Add authentication\"");
            AnsiConsole.MarkupLine("  xkode agent --plan plan.md");
            return 1;
        }

        // ── Validate Ollama connection ───────────────────────
        AnsiConsole.Markup("[cyan]Connecting to Ollama...[/] ");
        var isAvailable = await ollama.IsAvailableAsync();

        if (!isAvailable)
        {
            AnsiConsole.MarkupLine("[bold red]✗[/]");
            ollama.PrintConnectionHelp();
            return 1;
        }

        AnsiConsole.MarkupLine("[bold green]✓[/]");

        // Set models
        var model = settings.Model ?? config.DefaultModel;
        ollama.SetModel(model);

        // ── Initialize agents ────────────────────────────────
        var plannerModel = settings.PlannerModel ?? model;
        var executorModel = settings.ExecutorModel ?? model;
        var reviewerModel = settings.ReviewerModel ?? model;

        AnsiConsole.MarkupLine(
            $"[grey]Models:[/] " +
            $"Planner: [cyan]{plannerModel}[/], " +
            $"Executor: [cyan]{executorModel}[/], " +
            $"Reviewer: [cyan]{reviewerModel}[/]");

        var planner = new PlannerAgent(ollama, config);
        var executor = new ExecutorAgent(ollama, config, codeIndex, fileService);
        var reviewer = new ReviewerAgent(ollama, config);

        var orchestrator = new AgentOrchestrator(planner, executor, reviewer, fileService, config);

        // ── Load SKILL.md if present ─────────────────────────
        var projectRoot = System.IO.Path.GetFullPath(settings.Path);
        string skillContent = "";

        if (config.AutoLoadSkill)
        {
            skillContent = AutoLoadSkillFiles(projectRoot);
            orchestrator.SetSkillContent(skillContent); // Pass SKILL to orchestrator
        }

        // Update orchestrator behavior based on --no-review flag
        if (settings.NoReview)
        {
            // Disable review in orchestrator (but keep SKILL for planner/executor)
            orchestrator.DisableReview();
        }

        // ── Plan workflow: Import, Create, or Export ─────────
        ExecutionPlan? plan = null;

        // Option 1: Load plan from file
        if (!string.IsNullOrWhiteSpace(settings.PlanFile))
        {
            AnsiConsole.MarkupLine($"[cyan]Loading plan from:[/] {settings.PlanFile}");
            
            if (!File.Exists(settings.PlanFile))
            {
                AnsiConsole.MarkupLine($"[red]Error: Plan file not found: {settings.PlanFile}[/]");
                return 1;
            }

            try
            {
                var markdown = File.ReadAllText(settings.PlanFile);
                plan = ExecutionPlanExtensions.FromMarkdown(markdown);
                AnsiConsole.MarkupLine($"[green]✓ Loaded plan with {plan.Steps.Count} steps[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error parsing plan file: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }
        }
        // Option 2: Create new plan
        else
        {
            AnsiConsole.MarkupLine($"\n[bold cyan]🤖 Creating execution plan...[/]");
            var newContext = BuildCodebaseContext(projectRoot, skillContent);
            
            try
            {
                plan = await planner.CreatePlanAsync(settings.Task, newContext);
            }
            catch (AgentException ex)
            {
                AnsiConsole.MarkupLine($"[red]Planning failed: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }

            // Option 2a: Export plan and exit
            if (!string.IsNullOrWhiteSpace(settings.ExportPlanFile))
            {
                var markdown = plan.ToMarkdown();
                File.WriteAllText(settings.ExportPlanFile, markdown);
                
                AnsiConsole.MarkupLine($"\n[green]✓ Plan exported to:[/] [cyan]{settings.ExportPlanFile}[/]");
                AnsiConsole.MarkupLine($"[grey]Edit the plan, then run:[/] xkode agent --plan {settings.ExportPlanFile}");
                return 0;
            }
        }

        // ── Execute the plan ──────────────────────────────────
        AnsiConsole.MarkupLine($"\n[bold cyan]🤖 Multi-Agent Mode[/]");
        
        if (!string.IsNullOrWhiteSpace(settings.Task))
        {
            AnsiConsole.MarkupLine($"[grey]Task:[/] {Markup.Escape(settings.Task)}");
        }
        else if (plan != null)
        {
            AnsiConsole.MarkupLine($"[grey]Task:[/] {Markup.Escape(plan.Goal)}");
        }
        
        AnsiConsole.MarkupLine($"[grey]Project:[/] {projectRoot}");

        if (settings.AutoApprove)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Auto-approve enabled - all changes will be applied automatically[/]");
        }

        var cts = new CancellationTokenSource();

        // Intercept Ctrl+C
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            AnsiConsole.MarkupLine("\n[yellow]⚠️  Cancelling...[/]");
        };

        try
        {
            OrchestratorResult result;

            // Execute with pre-made plan or create new one
            if (plan != null)
            {
                result = await orchestrator.ExecutePlanAsync(
                    plan,
                    projectRoot,
                    settings.AutoApprove,
                    cts.Token);
            }
            else
            {
                result = await orchestrator.ExecuteTaskAsync(
                    settings.Task ?? "",
                    projectRoot,
                    settings.AutoApprove,
                    cts.Token);
            }

            // ── Display final result ─────────────────────────
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold cyan]Result[/]").RuleStyle("cyan"));
            AnsiConsole.WriteLine();

            if (result.Cancelled)
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  Cancelled by user[/]");
                return 2;
            }

            if (result.Success)
            {
                AnsiConsole.MarkupLine("[bold green]✓ Task completed successfully![/]");

                if (result.Plan != null)
                {
                    AnsiConsole.MarkupLine(
                        $"[grey]Completed {result.Plan.CompletedSteps}/{result.Plan.TotalSteps} steps[/]");
                }

                if (result.FinalReview != null)
                {
                    AnsiConsole.MarkupLine(
                        $"[grey]Final score: {result.FinalReview.Score}/10[/]");
                }

                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[bold red]✗ Task failed[/]");

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    AnsiConsole.MarkupLine($"[red]{Markup.Escape(result.Error)}[/]");
                }

                return 1;
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("\n[yellow]⚠️  Operation cancelled[/]");
            return 2;
        }
        catch (AgentException ex)
        {
            AnsiConsole.MarkupLine($"\n[bold red]Agent Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[bold red]Error:[/] {Markup.Escape(ex.Message)}");
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            // Restore config
            if (settings.NoReview)
            {
                config.AutoLoadSkill = true;
            }
        }
    }

    // ── Auto-load SKILL.md ───────────────────────────────────
    private string AutoLoadSkillFiles(string projectRoot)
    {
        var skillFiles = markdownService.FindSkillFiles(projectRoot);
        if (skillFiles.Count == 0) return "";

        AnsiConsole.MarkupLine("\n[cyan]Loading SKILL files...[/]");
        
        var skillContent = new System.Text.StringBuilder();
        foreach (var path in skillFiles)
        {
            var md = markdownService.ReadMarkdown(path);
            if (md == null) continue;

            var rel = System.IO.Path.GetRelativePath(projectRoot, path);
            AnsiConsole.MarkupLine($"  [green]✓[/] {rel}");
            
            // Accumulate SKILL content
            skillContent.AppendLine($"\n=== SKILL: {rel} ===");
            skillContent.AppendLine(md.RawContent);
            skillContent.AppendLine("=== END SKILL ===\n");
        }
        
        return skillContent.ToString();
    }

    // ── Build codebase context ───────────────────────────────
    private string BuildCodebaseContext(string projectRoot, string skillContent = "")
    {
        var ctx = fileService.IndexProject(projectRoot, config.MaxContextFiles);
        var codebaseContext = ctx.ToPromptContext(maxChars: 30_000);
        
        // Prepend SKILL content if available
        if (!string.IsNullOrWhiteSpace(skillContent))
        {
            return skillContent + "\n\n" + codebaseContext;
        }
        
        return codebaseContext;
    }
}
