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
        [Description("Task to execute (optional - starts interactive mode if not provided)")]
        public string? Task { get; set; }

        [CommandOption("--plan <FILE>")]
        [Description("Load execution plan from markdown file")]
        public string? PlanFile { get; set; }

        [CommandOption("--export-plan")]
        [Description("Export plan to markdown file and exit (auto-generates filename)")]
        public bool ExportPlan { get; set; }

        [CommandOption("--export-plan-file <FILE>")]
        [Description("Export plan to specific markdown file")]
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

        // Agent mode is now ALWAYS interactive
        // No validation needed - task is optional

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

        // ── Start Interactive REPL (ALWAYS) ──────────────────
        var replService = new AgentReplService(
            planner, 
            executor, 
            reviewer, 
            orchestrator, 
            config,
            skillLoaded: !string.IsNullOrWhiteSpace(skillContent),
            noReview: settings.NoReview);
        
        // Pass initial task if provided
        return await replService.RunAsync(projectRoot, settings.AutoApprove, settings.Task);
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

    // ── Generate plan filename from task description ─────────
    private static string GeneratePlanFilename(string task, string projectRoot)
    {
        // Summarize task (first 50 chars, clean up)
        var summary = task.Length > 50 ? task[..50] : task;

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        summary = string.Join("_", summary.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Replace spaces with hyphens, remove multiple spaces/hyphens
        summary = System.Text.RegularExpressions.Regex.Replace(summary, @"\s+", "-");
        summary = System.Text.RegularExpressions.Regex.Replace(summary, @"-+", "-");
        summary = summary.Trim('-').ToLower();

        // Add timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        var filename = $"plan-{summary}-{timestamp}.md";

        // Combine with project root
        return Path.Combine(projectRoot, filename);
    }
}
