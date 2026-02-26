using Spectre.Console;
using XKode.Agents;
using XKode.Models;
using XKode.Services;

namespace XKode.Services;

/// <summary>
/// Interactive REPL service for agent mode
/// </summary>
public class AgentReplService
{
    private readonly PlannerAgent _planner;
    private readonly ExecutorAgent _executor;
    private readonly ReviewerAgent _reviewer;
    private readonly AgentOrchestrator _orchestrator;
    private readonly ConfigService _config;
    private readonly AgentSession _session;
    private readonly bool _skillLoaded;
    private readonly bool _noReview;

    public AgentReplService(
        PlannerAgent planner,
        ExecutorAgent executor,
        ReviewerAgent reviewer,
        AgentOrchestrator orchestrator,
        ConfigService config,
        bool skillLoaded = false,
        bool noReview = false)
    {
        _planner = planner;
        _executor = executor;
        _reviewer = reviewer;
        _orchestrator = orchestrator;
        _config = config;
        _session = new AgentSession();
        _skillLoaded = skillLoaded;
        _noReview = noReview;
    }

    /// <summary>
    /// Start interactive REPL
    /// </summary>
    public async Task<int> RunAsync(string projectRoot, bool autoApprove = false, string? initialTask = null)
    {
        _session.ProjectRoot = projectRoot;
        _session.IsRunning = true;

        ShowWelcome(autoApprove);

        // Execute initial task if provided
        if (!string.IsNullOrWhiteSpace(initialTask))
        {
            AnsiConsole.MarkupLine($"[grey]Executing initial task...[/]\n");
            await ProcessAgentRequest(initialTask, autoApprove);
            AnsiConsole.WriteLine();
        }

        while (_session.IsRunning)
        {
            try
            {
                var input = ReadInput();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                _session.AddToHistory($"User: {input}");

                if (input.StartsWith("/"))
                {
                    await ProcessCommand(input);
                }
                else
                {
                    await ProcessAgentRequest(input, autoApprove);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            }
        }

        ShowGoodbye();
        return 0;
    }

    // ─── UI Methods ────────────────────────────────────────

    private void ShowWelcome(bool autoApprove)
    {
        AnsiConsole.Clear();
        
        var skillStatus = _skillLoaded ? "[green]✓ Loaded[/]" : "[grey]Not found[/]";
        var reviewStatus = _noReview ? "[yellow]Disabled[/]" : "[green]Enabled[/]";
        var approveStatus = autoApprove ? "[yellow]⚠️  Auto-approve ON[/]" : "[grey]Manual approval[/]";
        
        var panel = new Panel(
            new Markup($"""
                [bold cyan]XKode Agent Mode[/] [grey]v0.3.0[/]
                Interactive multi-agent assistant

                [bold]Current Agent:[/] [cyan]{_session.CurrentAgent}[/]
                [bold]Project:[/] [grey]{_session.ProjectRoot}[/]
                [bold]SKILL.md:[/] {skillStatus}
                [bold]Code Review:[/] {reviewStatus}
                [bold]Auto-approve:[/] {approveStatus}
                [bold]Default Model:[/] [cyan]{_config.DefaultModel}[/]

                [grey]Commands: /help /switch /agents /plan /status /config /exit[/]
                [grey]Type your request or command...[/]
                """))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private string ReadInput()
    {
        AnsiConsole.Markup($"[cyan]Agent[/] [[{_session.CurrentAgent}]] > ");
        return Console.ReadLine() ?? "";
    }

    private void ShowGoodbye()
    {
        AnsiConsole.MarkupLine($"\n[cyan]Session ended.[/] Duration: {_session.Duration:mm\\:ss}");
        AnsiConsole.MarkupLine($"[grey]Total interactions: {_session.History.Count}[/]");
    }

    // ─── Command Processing ────────────────────────────────

    private async Task ProcessCommand(string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var args = parts.Length > 1 ? parts[1] : "";

        switch (command)
        {
            case "/help":
                ShowHelp();
                break;

            case "/switch":
                SwitchAgent(args);
                break;

            case "/agents":
                ShowAgents();
                break;

            case "/plan":
                ShowCurrentPlan();
                break;

            case "/export":
                ExportPlan(args);
                break;

            case "/status":
                ShowStatus();
                break;

            case "/config":
                ShowConfig();
                break;

            case "/history":
                ShowHistory();
                break;

            case "/clear":
                ClearHistory();
                break;

            case "/exit":
            case "/quit":
                _session.IsRunning = false;
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}");
                AnsiConsole.MarkupLine("[grey]Type /help for available commands[/]");
                break;
        }
    }

    private void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]Command[/]")
            .AddColumn("[bold]Description[/]");

        table.AddRow("/switch <agent>", "Switch to different agent (planner, executor, reviewer)");
        table.AddRow("/agents", "List available agents");
        table.AddRow("/plan", "Show current execution plan");
        table.AddRow("/export <file>", "Export current plan to markdown file");
        table.AddRow("/status", "Show session status");
        table.AddRow("/config", "Show current configuration");
        table.AddRow("/history", "Show conversation history");
        table.AddRow("/clear", "Clear conversation history");
        table.AddRow("/exit", "Exit agent mode");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private void SwitchAgent(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            AnsiConsole.MarkupLine("[red]Usage: /switch <agent>[/]");
            AnsiConsole.MarkupLine("[grey]Available: planner, executor, reviewer[/]");
            return;
        }

        agentName = agentName.ToLower();
        
        if (agentName != "planner" && agentName != "executor" && agentName != "reviewer")
        {
            AnsiConsole.MarkupLine($"[red]Unknown agent:[/] {agentName}");
            AnsiConsole.MarkupLine("[grey]Available: planner, executor, reviewer[/]");
            return;
        }

        _session.CurrentAgent = agentName;
        AnsiConsole.MarkupLine($"[green]✓ Switched to[/] [cyan]{agentName}[/] agent");
    }

    private void ShowAgents()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]Agent[/]")
            .AddColumn("[bold]Description[/]")
            .AddColumn("[bold]Model[/]");

        table.AddRow(
            "[cyan]planner[/]",
            "Strategic planning, breaks tasks into steps",
            "[grey]qwen2.5-coder:32b[/]"
        );

        table.AddRow(
            "[cyan]executor[/]",
            "Code implementation, executes steps",
            "[grey]qwen2.5-coder:7b[/]"
        );

        table.AddRow(
            "[cyan]reviewer[/]",
            "Code review, finds bugs and issues",
            "[grey]qwen2.5-coder:32b[/]"
        );

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[grey]Current: {_session.CurrentAgent}[/]");
    }

    private void ShowCurrentPlan()
    {
        if (_session.CurrentPlan == null)
        {
            AnsiConsole.MarkupLine("[yellow]No active plan[/]");
            return;
        }

        var plan = _session.CurrentPlan;
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title($"[bold cyan]Current Plan[/] [grey]({plan.TotalSteps} steps)[/]")
            .AddColumn("#")
            .AddColumn("Description")
            .AddColumn("Status");

        foreach (var step in plan.Steps)
        {
            var status = step.Completed ? "[green]✓ Done[/]" :
                        step.Skipped ? "[yellow]⊘ Skipped[/]" :
                        "[grey]Pending[/]";

            table.AddRow(
                step.Order.ToString(),
                Markup.Escape(step.Description),
                status
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[bold]Goal:[/] {Markup.Escape(plan.Goal)}");
        AnsiConsole.MarkupLine($"[grey]Completed: {_session.CompletedSteps}/{plan.TotalSteps}[/]");
    }

    private void ExportPlan(string filename)
    {
        if (_session.CurrentPlan == null)
        {
            AnsiConsole.MarkupLine("[yellow]No active plan to export[/]");
            return;
        }

        if (string.IsNullOrWhiteSpace(filename))
        {
            filename = $"plan-{DateTime.Now:yyyyMMdd-HHmmss}.md";
        }

        var markdown = _session.CurrentPlan.ToMarkdown();
        var fullPath = Path.Combine(_session.ProjectRoot, filename);
        
        File.WriteAllText(fullPath, markdown);
        
        AnsiConsole.MarkupLine($"[green]✓ Plan exported to:[/] [cyan]{fullPath}[/]");
    }

    private void ShowStatus()
    {
        var skillStatus = _skillLoaded ? "[green]✓ Loaded[/]" : "[grey]Not found[/]";
        var reviewStatus = _noReview ? "[yellow]Disabled[/]" : "[green]Enabled[/]";
        
        var panel = new Panel(
            new Markup($"""
                [bold]Session Status[/]

                [bold]Agent:[/] [cyan]{_session.CurrentAgent}[/]
                [bold]Model:[/] [cyan]{_config.DefaultModel}[/]
                [bold]SKILL.md:[/] {skillStatus}
                [bold]Code Review:[/] {reviewStatus}
                [bold]Plan:[/] {(_session.CurrentPlan != null ? $"Active ({_session.CurrentPlan.TotalSteps} steps)" : "None")}
                [bold]Completed:[/] {_session.CompletedSteps}/{_session.CurrentPlan?.TotalSteps ?? 0} steps
                [bold]History:[/] {_session.History.Count} interactions
                [bold]Duration:[/] {_session.Duration:hh\\:mm\\:ss}
                [bold]Project:[/] [grey]{_session.ProjectRoot}[/]
                """))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
    }

    private void ShowConfig()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[bold cyan]Configuration[/]")
            .AddColumn("[bold]Setting[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("Default Model", _config.DefaultModel);
        table.AddRow("Ollama URL", _config.OllamaUrl);
        table.AddRow("Max Context Files", _config.MaxContextFiles.ToString());
        table.AddRow("Auto Load SKILL", _config.AutoLoadSkill ? "[green]Yes[/]" : "[grey]No[/]");
        table.AddRow("SKILL Loaded", _skillLoaded ? "[green]Yes[/]" : "[grey]No[/]");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[grey]To change config, edit ~/.xkode/config.json or use 'xkode config'[/]");
    }

    private void ShowHistory()
    {
        if (_session.History.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No history yet[/]");
            return;
        }

        AnsiConsole.MarkupLine("[bold]Conversation History[/]\n");
        
        foreach (var entry in _session.History.TakeLast(20))
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(entry)}[/]");
        }

        if (_session.History.Count > 20)
        {
            AnsiConsole.MarkupLine($"\n[grey]... and {_session.History.Count - 20} more[/]");
        }
    }

    private void ClearHistory()
    {
        _session.History.Clear();
        AnsiConsole.MarkupLine("[green]✓ History cleared[/]");
    }

    // ─── Agent Request Processing ──────────────────────────

    private async Task ProcessAgentRequest(string request, bool autoApprove)
    {
        AnsiConsole.MarkupLine($"\n[cyan]Processing with {_session.CurrentAgent} agent...[/]\n");

        try
        {
            var result = await _orchestrator.ExecuteTaskAsync(
                request,
                _session.ProjectRoot,
                autoApprove);

            _session.CurrentPlan = result.Plan;
            
            if (result.Plan != null)
            {
                _session.CompletedSteps = result.Plan.Steps.Count(s => s.Completed);
                
                // Auto-save plan to docs/plans/
                await AutoSavePlan(result.Plan);
            }

            if (result.Success)
            {
                AnsiConsole.MarkupLine("\n[green]✓ Task completed successfully![/]");
            }
            else if (result.Cancelled)
            {
                AnsiConsole.MarkupLine("\n[yellow]Task cancelled[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"\n[red]Task failed: {Markup.Escape(result.Error ?? "Unknown error")}[/]");
            }

            _session.AddToHistory($"Agent: {(result.Success ? "Success" : "Failed")}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]Error: {Markup.Escape(ex.Message)}[/]");
            _session.AddToHistory($"Error: {ex.Message}");
        }

        AnsiConsole.WriteLine();
    }

    // ─── Auto-save Methods ─────────────────────────────────

    private async Task AutoSavePlan(ExecutionPlan plan)
    {
        try
        {
            // Create docs/plans directory
            var plansDir = Path.Combine(_session.ProjectRoot, "docs", "plans");
            Directory.CreateDirectory(plansDir);

            // Generate filename from goal
            var sanitizedGoal = SanitizeFilename(plan.Goal);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var filename = $"{sanitizedGoal}-{timestamp}.md";
            var fullPath = Path.Combine(plansDir, filename);

            // Save plan
            var markdown = plan.ToMarkdown();
            await File.WriteAllTextAsync(fullPath, markdown);

            var relativePath = Path.GetRelativePath(_session.ProjectRoot, fullPath);
            AnsiConsole.MarkupLine($"[grey]📄 Plan saved: {relativePath}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Could not save plan: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static string SanitizeFilename(string input)
    {
        // Take first 50 chars
        var sanitized = input.Length > 50 ? input[..50] : input;
        
        // Remove invalid chars
        var invalidChars = Path.GetInvalidFileNameChars();
        sanitized = string.Join("-", sanitized.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Clean up
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s+", "-");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"-+", "-");
        sanitized = sanitized.Trim('-').ToLower();
        
        return sanitized;
    }
}
