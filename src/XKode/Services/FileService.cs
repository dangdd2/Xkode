using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace XKode.Services;

// ─────────────────────────────────────────────────────────────
//  FileService — Read, write, diff, and index project files
// ─────────────────────────────────────────────────────────────
public class FileService
{
    private static readonly HashSet<string> CodeExtensions =
    [
        ".cs", ".fs", ".vb",                           // .NET
        ".ts", ".tsx", ".js", ".jsx", ".mjs",          // JS/TS
        ".py", ".rb", ".go", ".rs", ".java", ".kt",    // Other langs
        ".cpp", ".c", ".h", ".hpp",                    // C/C++
        ".sql", ".graphql", ".proto",                  // Data
        ".json", ".yaml", ".yml", ".toml", ".xml",     // Config
        ".md", ".txt", ".env.example",                 // Docs
        ".sh", ".bash", ".ps1", ".cmd",                // Scripts
        ".html", ".css", ".scss", ".razor", ".cshtml"  // Web
    ];

    private static readonly HashSet<string> IgnoreDirs =
    [
        ".git", ".vs", ".idea", "node_modules",
        "bin", "obj", "__pycache__", ".venv",
        "dist", "build", "coverage", ".next"
    ];

    // ── Read a single file ──────────────────────────────────
    public string? ReadFile(string path)
    {
        if (!File.Exists(path)) return null;
        try { return File.ReadAllText(path); }
        catch { return null; }
    }

    // ── Write a file (creates dirs if needed) ──────────────
    public bool WriteFile(string path, string content)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, content);
            return true;
        }
        catch { return false; }
    }

    // ── Index a project directory — returns file summaries ──
    public ProjectContext IndexProject(string rootPath, int maxFiles = 80)
    {
        var files = new List<ProjectFile>();
        var allFiles = EnumerateCodeFiles(rootPath).Take(maxFiles).ToList();

        foreach (var filePath in allFiles)
        {
            var relativePath = Path.GetRelativePath(rootPath, filePath);
            var content = ReadFile(filePath) ?? "";
            var lines = content.Split('\n').Length;

            files.Add(new ProjectFile
            {
                Path = relativePath,
                FullPath = filePath,
                Extension = Path.GetExtension(filePath),
                Lines = lines,
                // Only include full content for small files
                Content = lines <= 500 ? content : TruncateWithSummary(content, relativePath)
            });
        }

        return new ProjectContext
        {
            RootPath = rootPath,
            Files = files,
            TotalFiles = allFiles.Count,
            Structure = BuildTreeString(rootPath)
        };
    }

    // ── Generate a unified diff between old and new content ─
    public string GenerateDiff(string originalContent, string newContent, string filePath)
    {
        var original = originalContent.Split('\n');
        var updated = newContent.Split('\n');

        var sb = new StringBuilder();
        sb.AppendLine($"--- a/{filePath}");
        sb.AppendLine($"+++ b/{filePath}");

        // Simple line-by-line diff
        int i = 0, j = 0;
        while (i < original.Length || j < updated.Length)
        {
            if (i < original.Length && j < updated.Length && original[i] == updated[j])
            {
                sb.AppendLine($"  {original[i]}");
                i++; j++;
            }
            else if (j < updated.Length &&
                     (i >= original.Length || !original.Contains(updated[j])))
            {
                sb.AppendLine($"+ {updated[j]}");
                j++;
            }
            else if (i < original.Length)
            {
                sb.AppendLine($"- {original[i]}");
                i++;
            }
        }

        return sb.ToString();
    }

    // ── Ask user to confirm before applying file changes ────
    public async Task<bool> ConfirmAndApplyEdit(
        string filePath,
        string newContent,
        bool autoAccept = false)
    {
        var original = ReadFile(filePath) ?? "";
        var diff = GenerateDiff(original, newContent, filePath);

        AnsiConsole.MarkupLine($"\n[bold yellow]📝 Proposed changes to:[/] [cyan]{filePath}[/]");

        // Show diff with color
        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith('+') && !line.StartsWith("+++"))
                AnsiConsole.MarkupLine($"[green]{Markup.Escape(line)}[/]");
            else if (line.StartsWith('-') && !line.StartsWith("---"))
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(line)}[/]");
            else
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
        }

        if (autoAccept) return WriteFile(filePath, newContent);

        var confirm = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[bold]Apply these changes?[/]")
                .AddChoices("✅ Yes, apply", "❌ No, skip", "👁 View full file"));

        if (confirm.StartsWith("👁"))
        {
            AnsiConsole.Write(new Panel(Markup.Escape(newContent))
                .Header($"[cyan]{filePath}[/]")
                .Border(BoxBorder.Rounded));

            confirm = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Apply?[/]")
                    .AddChoices("✅ Yes, apply", "❌ No, skip"));
        }

        if (confirm.StartsWith("✅"))
            return WriteFile(filePath, newContent);

        return false;
    }

    // ── Build file tree string ───────────────────────────────
    public string BuildTreeString(string rootPath, int maxDepth = 3)
    {
        var sb = new StringBuilder();
        BuildTree(rootPath, sb, "", 0, maxDepth);
        return sb.ToString();
    }

    // ─── Private helpers ────────────────────────────────────
    private IEnumerable<string> EnumerateCodeFiles(string root)
    {
        if (!Directory.Exists(root)) yield break;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var dir = Path.GetDirectoryName(file) ?? "";
            if (IgnoreDirs.Any(d => dir.Contains($"{Path.DirectorySeparatorChar}{d}") ||
                                    dir.Contains($"{d}{Path.DirectorySeparatorChar}")))
                continue;

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (CodeExtensions.Contains(ext))
                yield return file;
        }
    }

    private static string TruncateWithSummary(string content, string path)
    {
        var lines = content.Split('\n');
        var preview = string.Join('\n', lines.Take(50));
        return $"{preview}\n\n... [File truncated — {lines.Length} total lines] ...";
    }

    private void BuildTree(string dir, StringBuilder sb, string indent, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        var name = Path.GetFileName(dir);
        if (IgnoreDirs.Contains(name)) return;

        if (depth > 0)
            sb.AppendLine($"{indent}📁 {name}/");

        var files = Directory.EnumerateFiles(dir)
            .Where(f => CodeExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Take(10);

        foreach (var f in files)
            sb.AppendLine($"{indent}  📄 {Path.GetFileName(f)}");

        foreach (var subDir in Directory.EnumerateDirectories(dir).Take(8))
            BuildTree(subDir, sb, indent + "  ", depth + 1, maxDepth);
    }
}

// ─────────────────────────────────────────────────────────────
//  Project context passed to AI
// ─────────────────────────────────────────────────────────────
public class ProjectContext
{
    public string RootPath { get; set; } = "";
    public List<ProjectFile> Files { get; set; } = [];
    public int TotalFiles { get; set; }
    public string Structure { get; set; } = "";

    public string ToPromptContext(int maxChars = 60_000)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PROJECT STRUCTURE ===");
        sb.AppendLine(Structure);
        sb.AppendLine("\n=== FILE CONTENTS ===");

        foreach (var file in Files)
        {
            if (sb.Length > maxChars) break;
            sb.AppendLine($"\n--- {file.Path} ({file.Lines} lines) ---");
            sb.AppendLine(file.Content);
        }

        return sb.ToString();
    }
}

public class ProjectFile
{
    public string Path { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Extension { get; set; } = "";
    public int Lines { get; set; }
    public string Content { get; set; } = "";
}
