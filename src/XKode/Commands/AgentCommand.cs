using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using XKode.Agents;
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
        [CommandArgument(0, "<task>")]
        [Description("Task to execute using multi-agent workflow")]
        public string Task { get; set; } = "";

        [CommandOption("-p|--path")]
        [Description("Project root path (default: current directory)")]
        [DefaultValue(".")]
        public string Path { get; set; } = ".";

        [CommandOption("-m|--model")]
        [Description("Ollama model to use (default from config)")]
        public string? Model { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Auto-approve all steps (dangerous!)")]
        public bool AutoApprove { get; set; } = true;

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

        if (string.IsNullOrWhiteSpace(settings.Task))
        {
            AnsiConsole.MarkupLine("[red]Error: Task is required[/]");
            AnsiConsole.MarkupLine("[grey]Usage: xkode agent \"Add authentication\"[/]");
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

        // Update reviewer behavior based on --no-review flag
        if (settings.NoReview)
        {
            config.AutoLoadSkill = false; // Temporary disable for this run
        }

        var orchestrator = new AgentOrchestrator(planner, executor, reviewer, fileService, config);

        // ── Load SKILL.md if present ─────────────────────────
        var projectRoot = System.IO.Path.GetFullPath(settings.Path);
        if (config.AutoLoadSkill && !settings.NoReview)
        {
            AutoLoadSkillFiles(projectRoot);
        }

        // ── Execute multi-agent workflow ─────────────────────
        AnsiConsole.MarkupLine($"\n[bold cyan]🤖 Multi-Agent Mode[/]");
        AnsiConsole.MarkupLine($"[grey]Task:[/] {Markup.Escape(settings.Task)}");
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
            var result = await orchestrator.ExecuteTaskAsync(
                settings.Task,
                projectRoot,
                settings.AutoApprove,
                cts.Token);

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
    private void AutoLoadSkillFiles(string projectRoot)
    {
        var skillFiles = markdownService.FindSkillFiles(projectRoot);
        if (skillFiles.Count == 0) return;

        AnsiConsole.MarkupLine("\n[cyan]Loading SKILL files...[/]");
        foreach (var path in skillFiles)
        {
            var md = markdownService.ReadMarkdown(path);
            if (md == null) continue;

            var rel = System.IO.Path.GetRelativePath(projectRoot, path);
            AnsiConsole.MarkupLine($"  [green]✓[/] {rel}");
        }
    }
}
