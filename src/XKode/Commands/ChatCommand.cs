using XKode.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text;

namespace XKode.Commands;

// ─────────────────────────────────────────────────────────────
//  ChatCommand — Interactive REPL (the main feature)
//  New: /docs /skill /loaded — read & inject markdown files
// ─────────────────────────────────────────────────────────────
public class ChatCommand(
    OllamaService    ollama,
    FileService      fileService,
    CodeIndexService codeIndex,
    MarkdownService  markdownService) : AsyncCommand<ChatCommand.Settings>
{
    // Docs loaded via /docs or /skill during the session
    private readonly List<MarkdownFile> _loadedDocs = [];

    public class Settings : CommandSettings
    {
        [CommandOption("-m|--model")]
        [Description("Ollama model to use (default: qwen2.5-coder:7b)")]
        [DefaultValue("qwen2.5-coder:7b")]
        public string Model { get; set; } = "qwen2.5-coder:7b";

        [CommandOption("-p|--path")]
        [Description("Project root path (default: current directory)")]
        [DefaultValue(".")]
        public string Path { get; set; } = ".";

        [CommandOption("-y|--yes")]
        [Description("Auto-accept all file changes (dangerous!)")]
        public bool AutoAccept { get; set; } = false;

        [CommandOption("--no-context")]
        [Description("Don't load project files into context")]
        public bool NoContext { get; set; } = false;

        [CommandOption("--no-skill")]
        [Description("Don't auto-load SKILL.md on startup")]
        public bool NoSkill { get; set; } = false;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        Banner.Show();

        // ── Validate Ollama connection ───────────────────────
        AnsiConsole.Markup("[cyan]Connecting to Ollama...[/] ");
        var isAvailable = await ollama.IsAvailableAsync();
        if (!isAvailable)
        {
            AnsiConsole.MarkupLine("[bold red]✗[/]");
            OllamaService.PrintConnectionHelp();
            return 1;
        }
        AnsiConsole.MarkupLine("[bold green]✓[/]");
        ollama.SetModel(settings.Model);
        AnsiConsole.MarkupLine($"[bold green]✓ Connected[/] — Model: [cyan]{settings.Model}[/]");

        // ── Load project context ─────────────────────────────
        var projectRoot = System.IO.Path.GetFullPath(settings.Path);
        ProjectContext? ctx = null;

        if (!settings.NoContext)
        {
            AnsiConsole.Markup($"[cyan]Indexing project:[/] {projectRoot}... ");
            ctx = fileService.IndexProject(projectRoot);
            AnsiConsole.MarkupLine($"[bold green]✓[/] [white]{ctx.TotalFiles}[/] files");
        }

        // ── Auto-load SKILL.md if present ───────────────────
        if (!settings.NoSkill)
            AutoLoadSkillFiles(projectRoot);

        AnsiConsole.WriteLine();

        // ── Build conversation history ───────────────────────
        var history = new List<ChatMessage>
        {
            new() { Role = "system", Content = BuildFullSystemPrompt(ctx) }
        };

        // ── REPL loop ────────────────────────────────────────
        AnsiConsole.MarkupLine("[grey dim]────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[grey dim]Commands: /help /docs /skill /loaded /model /exit[/]");
        AnsiConsole.MarkupLine("[grey dim]────────────────────────────────────────────────────[/]\n");

        while (true)
        {
            AnsiConsole.Markup("[bold green]you[/][grey] ❯[/] ");
            var input = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Handle slash commands
            var (slashResult, injectMessage) = await HandleSlashCommand(
                input, settings, ctx, projectRoot, history);

            if (slashResult == SlashResult.Exit) break;
            if (slashResult == SlashResult.Handled) continue;

            // Regular message or injected doc message
            var userContent = injectMessage ?? input;
            history.Add(new ChatMessage { Role = "user", Content = userContent });
            AnsiConsole.MarkupLine("\n[bold blue]ai[/][grey] ❯[/]");

            // ── Stream AI response ───────────────────────────
            var fullResponse = new StringBuilder();
            var cts = new CancellationTokenSource();

            try
            {
                await foreach (var chunk in ollama.ChatStreamAsync(history, cts.Token))
                {
                    Console.Write(chunk);
                    fullResponse.Append(chunk);
                }
                Console.WriteLine("\n");
            }
            catch (OllamaException ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(ex.Message)}");
                continue;
            }

            var response = fullResponse.ToString();
            history.Add(new ChatMessage { Role = "assistant", Content = response });

            // ── Execute any file/shell actions ───────────────
            var hasActions = response.Contains("```write:") ||
                             (response.Contains("```bash") && !response.Contains("```bash\n#"));

            if (hasActions)
            {
                AnsiConsole.MarkupLine("[bold yellow]⚡ Actions detected:[/]");
                var results = await codeIndex.ExecuteActionsAsync(
                    response, projectRoot, settings.AutoAccept, cts.Token);

                foreach (var r in results)
                {
                    var icon = r.Success ? "[green]✓[/]" : "[red]✗[/]";
                    AnsiConsole.MarkupLine(
                        $"  {icon} [{(r.Type == "file_write" ? "cyan" : "yellow")}]" +
                        $"{Markup.Escape(r.Path)}[/] — {Markup.Escape(r.Message)}");
                }
                AnsiConsole.WriteLine();
            }
        }

        AnsiConsole.MarkupLine("\n[grey]Session ended. Goodbye! 👋[/]");
        return 0;
    }

    // ── Auto-load SKILL.md files on startup ─────────────────
    private void AutoLoadSkillFiles(string projectRoot)
    {
        var skillFiles = markdownService.FindSkillFiles(projectRoot);
        if (skillFiles.Count == 0) return;

        foreach (var path in skillFiles)
        {
            var md = markdownService.ReadMarkdown(path);
            if (md == null) continue;

            _loadedDocs.Add(md);
            var rel = System.IO.Path.GetRelativePath(projectRoot, path);
            AnsiConsole.MarkupLine(
                $"[bold green]✓ SKILL loaded:[/] [cyan]{rel}[/] " +
                $"[grey]({md.Sections.Count} sections)[/]");
        }
    }

    // ── Build system prompt + all loaded docs ────────────────
    private string BuildFullSystemPrompt(ProjectContext? ctx)
    {
        var sb = new StringBuilder();
        sb.Append(codeIndex.BuildSystemPrompt(ctx));

        if (_loadedDocs.Count == 0) return sb.ToString();

        sb.AppendLine("\n\n## LOADED DOCUMENTS & SKILLS");
        sb.AppendLine("Follow any instructions in SKILL files carefully.\n");

        foreach (var doc in _loadedDocs)
            sb.AppendLine(markdownService.BuildContextBlock(
                doc, doc.IsSkillFile ? "SKILL" : "DOCUMENT"));

        return sb.ToString();
    }

    // ── Handle /commands ────────────────────────────────────
    private async Task<(SlashResult, string?)> HandleSlashCommand(
        string input,
        Settings settings,
        ProjectContext? ctx,
        string projectRoot,
        List<ChatMessage> history)
    {
        if (!input.StartsWith('/')) return (SlashResult.NotHandled, null);

        var parts = input.Split(' ', 2);
        var cmd   = parts[0].ToLower();
        var arg   = parts.Length > 1 ? parts[1].Trim() : "";

        switch (cmd)
        {
            case "/exit" or "/quit" or "/q":
                return (SlashResult.Exit, null);

            case "/help":
                ShowHelp();
                return (SlashResult.Handled, null);

            case "/clear":
                AnsiConsole.Clear();
                Banner.Show();
                return (SlashResult.Handled, null);

            case "/context":
                if (ctx != null)
                {
                    AnsiConsole.MarkupLine(
                        $"[cyan]Project:[/] {ctx.RootPath}  " +
                        $"[cyan]Files:[/] {ctx.TotalFiles}\n");
                    AnsiConsole.MarkupLine(Markup.Escape(ctx.Structure));
                }
                else AnsiConsole.MarkupLine("[grey]No project context loaded.[/]");
                return (SlashResult.Handled, null);

            case "/model":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    AnsiConsole.MarkupLine($"[cyan]Current:[/] {ollama.CurrentModel}");
                    AnsiConsole.MarkupLine("[grey]Usage: /model qwen2.5-coder:32b[/]");
                }
                else
                {
                    ollama.SetModel(arg);
                    AnsiConsole.MarkupLine($"[green]✓ Model:[/] [cyan]{arg}[/]");
                }
                return (SlashResult.Handled, null);

            case "/add":
                return (HandleAddFile(arg, projectRoot, history), null);

            case "/run":
                if (!string.IsNullOrWhiteSpace(arg))
                {
                    using var cts = new CancellationTokenSource();
                    await new ShellService().ExecuteAsync(arg, projectRoot, true, cts.Token);
                }
                return (SlashResult.Handled, null);

            // ── 📄 /docs — load any .md file ─────────────────
            case "/docs":
                return HandleDocs(arg, projectRoot);

            // ── 🎓 /skill — load SKILL.md ────────────────────
            case "/skill":
                return HandleSkill(arg, projectRoot);

            // ── 📋 /loaded — list loaded docs ────────────────
            case "/loaded":
                ShowLoadedDocs();
                return (SlashResult.Handled, null);

            default:
                AnsiConsole.MarkupLine($"[red]Unknown:[/] {cmd} — type [white]/help[/]");
                return (SlashResult.Handled, null);
        }
    }

    // ── /docs <file> ─────────────────────────────────────────
    private (SlashResult, string?) HandleDocs(string arg, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            var files = markdownService.FindMarkdownFiles(projectRoot);
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No .md files found.[/]");
                return (SlashResult.Handled, null);
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold cyan]Markdown Files[/]")
                .AddColumn("File").AddColumn("Path");

            foreach (var f in files)
                table.AddRow(
                    $"[cyan]{System.IO.Path.GetFileName(f)}[/]",
                    $"[grey]{System.IO.Path.GetRelativePath(projectRoot, f)}[/]");

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[grey dim]Load one: /docs README.md[/]");
            return (SlashResult.Handled, null);
        }

        var fullPath = System.IO.Path.IsPathRooted(arg)
            ? arg : System.IO.Path.Combine(projectRoot, arg);

        var md = markdownService.ReadMarkdown(fullPath);
        if (md == null)
        {
            AnsiConsole.MarkupLine($"[red]Not found:[/] {arg}");
            return (SlashResult.Handled, null);
        }

        _loadedDocs.Add(md);
        markdownService.PrintSummary(md);
        AnsiConsole.MarkupLine(
            $"[bold green]✓ Loaded:[/] [cyan]{md.FileName}[/] " +
            $"[grey]({md.WordCount} words, {md.Sections.Count} sections)[/]");

        var inject = "I've loaded this document for reference:\n\n" +
                     markdownService.BuildContextBlock(md, "DOCUMENT") +
                     "\n\nPlease confirm you've read it and summarize the key points.";

        return (SlashResult.InjectedMessage, inject);
    }

    // ── /skill <file> ────────────────────────────────────────
    private (SlashResult, string?) HandleSkill(string arg, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            var skills = markdownService.FindSkillFiles(projectRoot);
            if (skills.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No SKILL.md files found.[/]");
                AnsiConsole.MarkupLine("[grey dim]Create: .xkode/SKILL.md[/]");
                return (SlashResult.Handled, null);
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold cyan]SKILL Files[/]")
                .AddColumn("File").AddColumn("Path");

            foreach (var f in skills)
                table.AddRow(
                    "[bold cyan]SKILL.md[/]",
                    $"[grey]{System.IO.Path.GetRelativePath(projectRoot, f)}[/]");

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[grey dim]Load one: /skill SKILL.md[/]");
            return (SlashResult.Handled, null);
        }

        var fullPath = System.IO.Path.IsPathRooted(arg)
            ? arg : System.IO.Path.Combine(projectRoot, arg);

        var md = markdownService.ReadMarkdown(fullPath);
        if (md == null)
        {
            AnsiConsole.MarkupLine($"[red]Not found:[/] {arg}");
            AnsiConsole.MarkupLine("[grey dim]Common paths: SKILL.md, .xkode/SKILL.md[/]");
            return (SlashResult.Handled, null);
        }

        md.IsSkillFile = true;
        _loadedDocs.Add(md);
        markdownService.PrintSummary(md);
        AnsiConsole.MarkupLine(
            $"[bold green]✓ SKILL loaded:[/] [cyan]{md.FileName}[/] " +
            $"[grey]({md.WordCount} words)[/]");

        var inject = "A SKILL/INSTRUCTIONS file has been loaded. " +
                     "Follow these instructions carefully:\n\n" +
                     markdownService.BuildContextBlock(md, "SKILL") +
                     "\n\nConfirm you understand these instructions.";

        return (SlashResult.InjectedMessage, inject);
    }

    // ── /add <file> ──────────────────────────────────────────
    private SlashResult HandleAddFile(
        string arg, string projectRoot, List<ChatMessage> history)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            AnsiConsole.MarkupLine("[grey]Usage: /add path/to/file[/]");
            return SlashResult.Handled;
        }

        var fullPath = System.IO.Path.Combine(projectRoot, arg);

        // Markdown files get rich parsing
        if (arg.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var md = markdownService.ReadMarkdown(fullPath);
            if (md != null)
            {
                _loadedDocs.Add(md);
                history[0] = new ChatMessage
                {
                    Role    = "system",
                    Content = history[0].Content + "\n\n" +
                              markdownService.BuildContextBlock(md)
                };
                AnsiConsole.MarkupLine(
                    $"[green]✓ Added:[/] [cyan]{arg}[/] [grey]({md.WordCount} words)[/]");
                return SlashResult.Handled;
            }
        }

        // Regular code file
        var content = fileService.ReadFile(fullPath);
        if (content != null)
        {
            history[0] = new ChatMessage
            {
                Role    = "system",
                Content = history[0].Content +
                          $"\n\n=== ADDED FILE: {arg} ===\n{content}\n=== END ==="
            };
            AnsiConsole.MarkupLine($"[green]✓ Added:[/] [cyan]{arg}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Not found:[/] {arg}");
        }

        return SlashResult.Handled;
    }

    // ── /loaded ──────────────────────────────────────────────
    private void ShowLoadedDocs()
    {
        if (_loadedDocs.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No documents loaded. Use /docs or /skill.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold cyan]Loaded Documents[/]")
            .AddColumn("Type")
            .AddColumn("File")
            .AddColumn("Sections")
            .AddColumn("Words");

        foreach (var doc in _loadedDocs)
            table.AddRow(
                doc.IsSkillFile ? "[bold cyan]SKILL[/]" : "[white]DOC[/]",
                $"[cyan]{doc.FileName}[/]",
                doc.Sections.Count.ToString(),
                doc.WordCount.ToString());

        AnsiConsole.Write(table);
    }

    // ── Help ─────────────────────────────────────────────────
    private static void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold cyan]XKode — Commands[/]")
            .AddColumn("[bold cyan]Command[/]")
            .AddColumn("[bold white]Description[/]");

        table.AddRow("[bold white]DOCUMENTS[/]",          "");
        table.AddRow("/docs",                             "List all .md files in project");
        table.AddRow("/docs [cyan]README.md[/]",          "Load & inject markdown into AI context");
        table.AddRow("/skill",                            "List SKILL.md files in project");
        table.AddRow("/skill [cyan]SKILL.md[/]",          "Load instructions/skill file");
        table.AddRow("/loaded",                           "Show all loaded documents");
        table.AddRow("",                                  "");
        table.AddRow("[bold white]PROJECT[/]",            "");
        table.AddRow("/context",                          "Show indexed project files");
        table.AddRow("/add [cyan]<file>[/]",              "Add any file to AI context");
        table.AddRow("/run [cyan]<cmd>[/]",               "Run a shell command");
        table.AddRow("",                                  "");
        table.AddRow("[bold white]MODEL[/]",              "");
        table.AddRow("/model",                            "Show current model");
        table.AddRow("/model [cyan]<name>[/]",            "Switch Ollama model");
        table.AddRow("",                                  "");
        table.AddRow("[bold white]GENERAL[/]",            "");
        table.AddRow("/clear",                            "Clear screen");
        table.AddRow("/exit  /q",                         "Quit");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            "\n[grey dim]💡 Tip: Place [white].xkode/SKILL.md[/] " +
            "in your project to auto-load instructions on startup.[/]\n");
    }

    private enum SlashResult { NotHandled, Handled, InjectedMessage, Exit }
}
