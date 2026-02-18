using XKode.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text;

namespace XKode.Commands;

// ─────────────────────────────────────────────────────────────
//  RunCommand — Single-shot non-interactive task
//  Usage: xkode run "Add input validation to Program.cs"
// ─────────────────────────────────────────────────────────────
public class RunCommand(
    OllamaService ollama,
    FileService fileService,
    CodeIndexService codeIndex) : AsyncCommand<RunCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[task]")]
        [Description("The task to perform")]
        public string Task { get; set; } = "";

        [CommandOption("-m|--model")]
        [DefaultValue("qwen2.5-coder:7b")]
        public string Model { get; set; } = "qwen2.5-coder:7b";

        [CommandOption("-p|--path")]
        [DefaultValue(".")]
        public string Path { get; set; } = ".";

        [CommandOption("-y|--yes")]
        [Description("Auto-accept all changes without prompting")]
        public bool AutoAccept { get; set; } = false;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Task))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Please provide a task.");
            AnsiConsole.MarkupLine("[grey]Usage: xkode run \"Add logging to MyService.cs\"[/]");
            return 1;
        }

        ollama.SetModel(settings.Model);
        var projectRoot = System.IO.Path.GetFullPath(settings.Path);

        // Load context
        AnsiConsole.Markup("[cyan]Indexing project...[/] ");
        var ctx = fileService.IndexProject(projectRoot);
        AnsiConsole.MarkupLine($"[green]✓[/] {ctx.TotalFiles} files\n");
        AnsiConsole.MarkupLine($"[bold]Task:[/] {Markup.Escape(settings.Task)}\n");
        AnsiConsole.Write(new Rule("[grey]AI Response[/]").RuleStyle("grey"));

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = codeIndex.BuildSystemPrompt(ctx) },
            new() { Role = "user", Content = settings.Task }
        };

        var responseBuilder = new StringBuilder();
        await foreach (var chunk in ollama.ChatStreamAsync(messages))
        {
            Console.Write(chunk);
            responseBuilder.Append(chunk);
        }
        Console.WriteLine("\n");

        var response = responseBuilder.ToString();

        AnsiConsole.Write(new Rule("[grey]Actions[/]").RuleStyle("grey"));
        var results = await codeIndex.ExecuteActionsAsync(
            response, projectRoot, settings.AutoAccept);

        if (results.Count == 0)
            AnsiConsole.MarkupLine("[grey]No file changes or commands to execute.[/]");

        return 0;
    }
}

// ─────────────────────────────────────────────────────────────
//  ReviewCommand — AI code review
//  Usage: xkode review [file] or xkode review --path .
// ─────────────────────────────────────────────────────────────
public class ReviewCommand(
    OllamaService ollama,
    FileService fileService,
    CodeIndexService codeIndex) : AsyncCommand<ReviewCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[file]")]
        [Description("Specific file to review (optional)")]
        public string? File { get; set; }

        [CommandOption("-m|--model")]
        [DefaultValue("qwen2.5-coder:7b")]
        public string Model { get; set; } = "qwen2.5-coder:7b";

        [CommandOption("-p|--path")]
        [DefaultValue(".")]
        public string Path { get; set; } = ".";

        [CommandOption("--focus")]
        [Description("Focus area: security, performance, style, bugs (default: all)")]
        [DefaultValue("all")]
        public string Focus { get; set; } = "all";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ollama.SetModel(settings.Model);
        var projectRoot = System.IO.Path.GetFullPath(settings.Path);

        string codeToReview;
        string reviewTarget;

        if (!string.IsNullOrWhiteSpace(settings.File))
        {
            var fullPath = System.IO.Path.Combine(projectRoot, settings.File);
            codeToReview = fileService.ReadFile(fullPath) ?? "";
            reviewTarget = settings.File;

            if (string.IsNullOrEmpty(codeToReview))
            {
                AnsiConsole.MarkupLine($"[red]File not found:[/] {settings.File}");
                return 1;
            }
        }
        else
        {
            var ctx = fileService.IndexProject(projectRoot, maxFiles: 20);
            codeToReview = ctx.ToPromptContext();
            reviewTarget = projectRoot;
        }

        AnsiConsole.MarkupLine(
            $"[bold]Reviewing:[/] [cyan]{reviewTarget}[/] " +
            $"[grey](focus: {settings.Focus})[/]\n");

        var prompt = $"""
            Please perform a thorough code review of the following code.
            Focus: {settings.Focus}
            
            For each issue found, provide:
            1. Severity (Critical/High/Medium/Low)
            2. Location (file and line if possible)  
            3. Issue description
            4. Suggested fix with code example
            
            End with an overall summary and score (1-10).
            
            Code to review:
            {codeToReview}
            """;

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = codeIndex.BuildSystemPrompt() },
            new() { Role = "user", Content = prompt }
        };

        AnsiConsole.Write(new Rule("[bold cyan]Code Review Results[/]").RuleStyle("cyan"));
        Console.WriteLine();

        await foreach (var chunk in ollama.ChatStreamAsync(messages))
            Console.Write(chunk);

        Console.WriteLine("\n");
        return 0;
    }
}

// ─────────────────────────────────────────────────────────────
//  ModelsCommand — List available Ollama models
//  Usage: xkode models
// ─────────────────────────────────────────────────────────────
public class ModelsCommand(OllamaService ollama) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        AnsiConsole.Markup("[cyan]Fetching models from Ollama...[/] ");
        var models = await ollama.ListModelsAsync();
        AnsiConsole.MarkupLine(models.Count > 0 ? "[green]✓[/]" : "[yellow]done[/]");

        if (models.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No models found.[/]");
            AnsiConsole.MarkupLine("[grey]Install a model:[/] [cyan]ollama pull qwen2.5-coder:7b[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold cyan]Available Ollama Models[/]")
            .AddColumn("[bold]Model Name[/]")
            .AddColumn("[bold]Size[/]")
            .AddColumn("[bold]Last Modified[/]")
            .AddColumn("[bold]Recommended For[/]");

        foreach (var model in models)
        {
            var size = model.Size > 0
                ? $"{model.Size / 1_073_741_824.0:F1} GB"
                : "Unknown";

            var recommendation = GetRecommendation(model.Name);
            table.AddRow(
                $"[cyan]{model.Name}[/]",
                size,
                model.ModifiedAt.ToString("yyyy-MM-dd"),
                recommendation);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            $"\n[grey]Use a model:[/] [cyan]xkode chat --model [model-name][/]");
        AnsiConsole.MarkupLine(
            $"[grey]Pull a new model:[/] [cyan]ollama pull qwen2.5-coder:32b[/]");

        return 0;
    }

    private static string GetRecommendation(string name) => name.ToLower() switch
    {
        var n when n.Contains("qwen2.5-coder") && n.Contains("32b")
            => "⭐ Best quality coding",
        var n when n.Contains("qwen2.5-coder")
            => "⭐ Recommended — fast & capable",
        var n when n.Contains("deepseek-coder")
            => "🎯 Excellent code reasoning",
        var n when n.Contains("codellama")
            => "🔧 Code completion",
        var n when n.Contains("llama3")
            => "💬 General purpose / explain",
        var n when n.Contains("mistral")
            => "💬 Multilingual / general",
        _ => "General purpose"
    };
}
