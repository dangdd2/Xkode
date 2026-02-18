using System.Diagnostics;
using System.Text;
using Spectre.Console;

namespace XKode.Services;

// ─────────────────────────────────────────────────────────────
//  ShellService — Safe terminal command execution
// ─────────────────────────────────────────────────────────────
public class ShellService
{
    // Commands that are always blocked — no exceptions
    private static readonly HashSet<string> DangerousCommands =
    [
        "rm -rf /", "rm -rf ~", "mkfs", "dd if=/dev/zero",
        ":(){ :|:& };:", "chmod -R 777 /", "> /dev/sda",
        "curl | sh", "wget | sh", "shutdown", "reboot", "halt"
    ];

    // Commands requiring extra confirmation
    private static readonly HashSet<string> HighRiskPrefixes =
    [
        "rm ", "del ", "rmdir", "drop ", "truncate",
        "format ", "fdisk", "diskpart"
    ];

    // ── Execute with safety checks ──────────────────────────
    public async Task<ShellResult> ExecuteAsync(
        string command,
        string workingDir = ".",
        bool requireConfirmation = true,
        CancellationToken ct = default)
    {
        // Safety: block dangerous commands
        if (IsDangerous(command))
        {
            AnsiConsole.MarkupLine("[bold red]⛔ Blocked dangerous command:[/] " +
                Markup.Escape(command));
            return new ShellResult
            {
                Command = command,
                ExitCode = -1,
                Stderr = "Command blocked by safety filter",
                Blocked = true
            };
        }

        // Show what we're about to run
        AnsiConsole.MarkupLine(
            $"\n[bold yellow]🖥  Shell command:[/] [cyan]{Markup.Escape(command)}[/]");

        // Confirm if needed
        if (requireConfirmation && !await ConfirmAsync(command))
        {
            return new ShellResult
            {
                Command = command,
                ExitCode = 0,
                Stdout = "(skipped by user)",
                Skipped = true
            };
        }

        return await RunAsync(command, workingDir, ct);
    }

    // ── Run without confirmation (batch mode) ───────────────
    public async Task<ShellResult> RunAsync(
        string command,
        string workingDir = ".",
        CancellationToken ct = default)
    {
        var isWindows = OperatingSystem.IsWindows();
        var shell = isWindows ? "cmd.exe" : "/bin/bash";
        var args = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

        var psi = new ProcessStartInfo(shell, args)
        {
            WorkingDirectory = Directory.Exists(workingDir) ? workingDir : ".",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdoutSb.AppendLine(e.Data);
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(e.Data)}[/]");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderrSb.AppendLine(e.Data);
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(e.Data)}[/]");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        return new ShellResult
        {
            Command = command,
            ExitCode = process.ExitCode,
            Stdout = stdoutSb.ToString(),
            Stderr = stderrSb.ToString(),
            Success = process.ExitCode == 0
        };
    }

    // ── Parse AI output for shell commands ──────────────────
    public List<string> ExtractShellCommands(string aiResponse)
    {
        var commands = new List<string>();
        var inBlock = false;
        var blockLang = "";
        var blockLines = new List<string>();

        foreach (var line in aiResponse.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (!inBlock)
                {
                    inBlock = true;
                    blockLang = line.Trim().TrimStart('`').ToLower();
                    blockLines.Clear();
                }
                else
                {
                    if (blockLang is "bash" or "sh" or "shell" or "powershell" or "cmd" or "")
                        commands.AddRange(blockLines.Where(l => !l.TrimStart().StartsWith('#')));
                    inBlock = false;
                }
            }
            else if (inBlock)
            {
                blockLines.Add(line);
            }
        }

        return commands;
    }

    // ─── Private helpers ────────────────────────────────────
    private static bool IsDangerous(string cmd)
    {
        var normalized = cmd.ToLower().Trim();
        return DangerousCommands.Any(d => normalized.Contains(d));
    }

    private static async Task<bool> ConfirmAsync(string command)
    {
        var isHighRisk = HighRiskPrefixes.Any(p =>
            command.TrimStart().ToLower().StartsWith(p));

        if (isHighRisk)
        {
            AnsiConsole.MarkupLine("[bold red]⚠️  HIGH RISK COMMAND — please review carefully![/]");
        }

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Run this command?[/]")
                .AddChoices("✅ Yes, run it", "❌ No, skip"));

        return choice.StartsWith("✅");
    }
}

public class ShellResult
{
    public string Command { get; set; } = "";
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public bool Success { get; set; }
    public bool Blocked { get; set; }
    public bool Skipped { get; set; }

    public string Output => string.IsNullOrWhiteSpace(Stdout) ? Stderr : Stdout;
}
