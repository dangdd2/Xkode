using System.Text.RegularExpressions;
using Spectre.Console;

namespace XKode.Services;

// ─────────────────────────────────────────────────────────────
//  CodeIndexService — Parse AI responses and dispatch tool calls
//  The agent "brain" that routes actions from AI output
// ─────────────────────────────────────────────────────────────
public class CodeIndexService(FileService files, ShellService shell)
{
    private static readonly Dictionary<string, string> CodeLangDisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["js"]          = "JavaScript",
            ["javascript"]  = "JavaScript",
            ["ts"]          = "TypeScript",
            ["typescript"]  = "TypeScript",
            ["csharp"]      = "C#",
            ["cs"]          = "C#",
            ["c#"]          = "C#",
            ["py"]          = "Python",
            ["python"]      = "Python",
            ["ps"]          = "PowerShell",
            ["powershell"]  = "PowerShell",
            ["sh"]          = "Bash",
            ["bash"]        = "Bash",
            ["shell"]       = "Shell",
            ["cmd"]         = "Command Prompt",
            ["json"]        = "JSON",
            ["yaml"]        = "YAML",
            ["yml"]         = "YAML",
            ["toml"]        = "TOML",
            ["sql"]         = "SQL",
            ["html"]        = "HTML",
            ["css"]         = "CSS",
            ["xml"]         = "XML",
            ["c"]           = "C",
            ["cpp"]         = "C++",
            ["c++"]         = "C++",
            ["go"]          = "Go",
            ["golang"]      = "Go",
            ["rs"]          = "Rust",
            ["rust"]        = "Rust",
            ["php"]         = "PHP",
            ["rb"]          = "Ruby",
            ["ruby"]        = "Ruby",
            ["java"]        = "Java",
        };

    // ── Parse and execute any actions in AI response ────────
    public async Task<List<ActionResult>> ExecuteActionsAsync(
        string aiResponse,
        string projectRoot,
        bool autoAccept = false,
        CancellationToken ct = default)
    {
        var results = new List<ActionResult>();

        // 1. File writes — ```write:path/to/file.cs ... ```
        var fileWrites = ParseFileWrites(aiResponse);
        foreach (var (path, content) in fileWrites)
        {
            var fullPath = Path.Combine(projectRoot, path);
            AnsiConsole.MarkupLine($"\n[bold cyan]📝 File edit:[/] [yellow]{path}[/]");

            var applied = await files.ConfirmAndApplyEdit(fullPath, content, autoAccept);
            results.Add(new ActionResult
            {
                Type = "file_write",
                Path = path,
                Success = applied,
                Message = applied ? "Changes applied" : "Skipped by user"
            });
        }

        // 2. Shell commands — ```bash ... ```
        if (!autoAccept)
        {
            var commands = shell.ExtractShellCommands(aiResponse);
            foreach (var cmd in commands)
            {
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                var result = await shell.ExecuteAsync(cmd, projectRoot, true, ct);
                results.Add(new ActionResult
                {
                    Type = "shell",
                    Path = cmd,
                    Success = result.Success,
                    Message = result.Output
                });
            }
        }

        return results;
    }

    public static string FormatCodeHeader(string rawFenceInfo)
    {
        var info = rawFenceInfo.Trim();

        if (info.StartsWith("```", StringComparison.Ordinal))
            info = info.Trim('`').Trim();

        if (string.IsNullOrWhiteSpace(info))
            return "Code";

        if (CodeLangDisplayNames.TryGetValue(info, out var mapped))
            return $"Code — {mapped}";

        // Handle common prefixes / multi-token forms like "language-csharp hljs"
        var token = info;
        var spaceIndex = token.IndexOfAny(new[] { ' ', '\t' });
        if (spaceIndex > 0)
            token = token[..spaceIndex];

        if (token.StartsWith("language-", StringComparison.OrdinalIgnoreCase))
            token = token["language-".Length..];

        if (CodeLangDisplayNames.TryGetValue(token, out mapped))
            return $"Code — {mapped}";

        // Unknown language → generic header
        return "Code";
    }

    // ── Build the system prompt with project context ────────
    public string BuildSystemPrompt(ProjectContext? ctx = null)
    {
        var sb = new System.Text.StringBuilder();

        //You are XKode, an expert AI coding assistant running locally.
        sb.AppendLine("""
            You are an expert AI coding assistant running locally.
            When user asking you the model that you are using. you can answer the real model that you are using. 
            You have access to the user's codebase and can make changes to files, run commands, and review code.
            You help developers understand, write, edit, and debug code.
            
            ## CAPABILITIES
            You can:
            1. Read and understand entire codebases
            2. Write and edit files using the syntax below
            3. Run shell commands using bash code blocks
            4. Review code and suggest improvements
            5. Debug errors and fix issues
            
            ## REASONING FORMAT
            Before your main answer, briefly explain what you are about to do:

            Thinking...
            <1–3 short sentences explaining your plan>
            ...done thinking.

            Keep this reasoning concise and high-level.

            ## FILE EDITING SYNTAX
            To create or edit a file, use this exact format:
            ```write:path/to/file.ext
            [file content here]
            ```
            
            Always show the COMPLETE file content when editing.
            
            ## SHELL COMMANDS  
            Use standard bash code blocks for terminal commands:
            ```bash
            dotnet build
            ```
            
            ## GUIDELINES
            - Be concise but thorough
            - Always explain what you're doing before doing it
            - Show diffs or summaries for large file changes
            - Ask for clarification if the task is ambiguous
            - Prefer small, focused changes over large rewrites
            - After editing files, suggest how to test the changes
            """);

        if (ctx != null)
        {
            sb.AppendLine("\n## PROJECT CONTEXT");
            sb.AppendLine($"Root: {ctx.RootPath}");
            sb.AppendLine($"Files: {ctx.TotalFiles} code files found");
            sb.AppendLine("\n" + ctx.ToPromptContext());
        }

        return sb.ToString();
    }

    // ── Parse ```write:path``` blocks from AI response ──────
    private static List<(string Path, string Content)> ParseFileWrites(string response)
    {
        var result = new List<(string, string)>();
        var pattern = new Regex(
            @"```write:([^\n]+)\n(.*?)```",
            RegexOptions.Singleline);

        foreach (Match m in pattern.Matches(response))
        {
            var path = m.Groups[1].Value.Trim();
            var content = m.Groups[2].Value;
            // Remove leading newline if present
            if (content.StartsWith('\n')) content = content[1..];
            result.Add((path, content));
        }

        return result;
    }

    // ── Format AI response nicely for terminal display ──────
    public static void RenderMarkdown(string text)
    {
        var lines = text.Split('\n');
        bool inCode = false;
        string codeLang = "";
        var codeLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (!inCode)
                {
                    inCode = true;
                    codeLang = line.Trim().TrimStart('`');
                    codeLines.Clear();
                }
                else
                {
                    // Render code block
                    var codeContent = string.Join('\n', codeLines);
                    var headerText = FormatCodeHeader(codeLang);
                    AnsiConsole.Write(
                        new Panel(Markup.Escape(codeContent))
                            .Header($"[bold cyan]{Markup.Escape(headerText)}[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderColor(Color.Grey));
                    inCode = false;
                }
                continue;
            }

            if (inCode)
            {
                codeLines.Add(line);
                continue;
            }

            // Render markdown inline formatting
            var rendered = RenderInline(line);
            AnsiConsole.MarkupLine(rendered);
        }
    }

    private static string RenderInline(string line)
    {
        // Headers
        if (line.StartsWith("### "))
            return $"[bold yellow]{Markup.Escape(line[4..])}[/]";
        if (line.StartsWith("## "))
            return $"[bold cyan]{Markup.Escape(line[3..])}[/]";
        if (line.StartsWith("# "))
            return $"[bold white]{Markup.Escape(line[2..])}[/]";

        // Bullet points
        if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            return $"  [green]•[/] {Markup.Escape(line.TrimStart()[2..])}";

        // Bold **text**
        var result = Regex.Replace(line, @"\*\*(.+?)\*\*", m =>
            $"[bold]{Markup.Escape(m.Groups[1].Value)}[/]");

        // Inline `code`
        result = Regex.Replace(result, @"`([^`]+)`", m =>
            $"[cyan on grey15]{Markup.Escape(m.Groups[1].Value)}[/]");

        return result;
    }
}

public class ActionResult
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
