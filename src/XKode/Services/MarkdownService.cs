using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace XKode.Services;

// ─────────────────────────────────────────────────────────────
//  MarkdownService — Read, parse, and inject .md / SKILL.md
//  files into the AI conversation context.
//
//  Supports:
//    /docs README.md         — inject any markdown file
//    /skill SKILL.md         — inject a skill/instruction file
//    /docs                   — list all .md files in project
//    /skill                  — list discovered SKILL.md files
// ─────────────────────────────────────────────────────────────
public class MarkdownService
{
    // ── Standard SKILL.md search paths ─────────────────────
    private static readonly string[] SkillSearchPaths =
    [
        "SKILL.md",
        ".xkode/SKILL.md",
        "docs/SKILL.md",
        ".github/SKILL.md",
        "ai/SKILL.md",
    ];

    // ── Read & parse a markdown file ───────────────────────
    public MarkdownFile? ReadMarkdown(string path)
    {
        if (!File.Exists(path)) return null;

        var raw = File.ReadAllText(path);
        var sections = ParseSections(raw);
        var metadata = ParseFrontmatter(raw);

        return new MarkdownFile
        {
            Path        = path,
            FileName    = Path.GetFileName(path),
            RawContent  = raw,
            Sections    = sections,
            Metadata    = metadata,
            WordCount   = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            IsSkillFile = Path.GetFileName(path).Equals("SKILL.md",
                            StringComparison.OrdinalIgnoreCase)
                          || raw.Contains("## Instructions")
                          || raw.Contains("## Rules")
                          || raw.Contains("## Skills"),
        };
    }

    // ── Discover SKILL.md files in a project root ──────────
    public List<string> FindSkillFiles(string projectRoot)
    {
        var found = new List<string>();

        // Check standard paths
        foreach (var relative in SkillSearchPaths)
        {
            var full = Path.Combine(projectRoot, relative);
            if (File.Exists(full)) found.Add(full);
        }

        // Also scan recursively for any SKILL.md
        try
        {
            var scanned = Directory
                .EnumerateFiles(projectRoot, "SKILL.md", SearchOption.AllDirectories)
                .Where(p => !p.Contains(".git") && !p.Contains("node_modules"))
                .Where(p => !found.Contains(p));
            found.AddRange(scanned);
        }
        catch { /* ignore permission errors */ }

        return found.Distinct().ToList();
    }

    // ── Discover all .md files in a project ─────────────────
    public List<string> FindMarkdownFiles(string projectRoot, int max = 30)
    {
        try
        {
            return Directory
                .EnumerateFiles(projectRoot, "*.md", SearchOption.AllDirectories)
                .Where(p => !p.Contains(".git") && !p.Contains("node_modules")
                         && !p.Contains("bin")   && !p.Contains("obj"))
                .Take(max)
                .ToList();
        }
        catch { return []; }
    }

    // ── Build prompt injection text from a markdown file ────
    public string BuildContextBlock(MarkdownFile md, string label = "DOCUMENT")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {label}: {md.FileName} ===");

        if (md.IsSkillFile)
        {
            sb.AppendLine("(This is a SKILL/INSTRUCTIONS file — follow these rules carefully)");
        }

        sb.AppendLine(md.RawContent);
        sb.AppendLine($"=== END {label} ===");
        return sb.ToString();
    }

    // ── Pretty-print a markdown file to terminal ────────────
    public void PrintMarkdown(MarkdownFile md)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold cyan]{md.FileName}[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        foreach (var section in md.Sections)
        {
            // Section header
            var headerColor = section.Level switch
            {
                1 => "bold white",
                2 => "bold cyan",
                _ => "bold yellow",
            };
            AnsiConsole.MarkupLine($"[{headerColor}]{new string('#', section.Level)} {Markup.Escape(section.Title)}[/]");

            // Section body — render inline markdown
            foreach (var line in section.Body.Split('\n'))
                RenderLine(line);

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine($"[grey dim]{md.WordCount} words — {md.Sections.Count} sections[/]");
        AnsiConsole.WriteLine();
    }

    // ── Print a compact summary table of sections ───────────
    public void PrintSummary(MarkdownFile md)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]{md.FileName}[/] [grey]({md.WordCount} words)[/]")
            .AddColumn("[bold]#[/]")
            .AddColumn("[bold]Section[/]")
            .AddColumn("[bold]Preview[/]");

        for (int i = 0; i < md.Sections.Count; i++)
        {
            var s = md.Sections[i];
            var preview = s.Body.Split('\n')
                            .FirstOrDefault(l => l.Trim().Length > 0) ?? "";
            if (preview.Length > 60) preview = preview[..60] + "...";

            table.AddRow(
                $"[grey]{i + 1}[/]",
                $"[cyan]{new string('#', s.Level)} {Markup.Escape(s.Title)}[/]",
                $"[grey]{Markup.Escape(preview)}[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    // ─── Private: parse markdown into sections ──────────────
    private static List<MarkdownSection> ParseSections(string content)
    {
        var sections = new List<MarkdownSection>();
        var headerPattern = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
        var matches = headerPattern.Matches(content).ToList();

        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var start = m.Index + m.Length;
            var end   = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            var body  = content[start..end].Trim();

            sections.Add(new MarkdownSection
            {
                Level = m.Groups[1].Value.Length,
                Title = m.Groups[2].Value.Trim(),
                Body  = body,
            });
        }

        // If no headers, treat whole file as one section
        if (sections.Count == 0)
            sections.Add(new MarkdownSection { Level = 1, Title = "Content", Body = content });

        return sections;
    }

    // ─── Private: parse YAML frontmatter ────────────────────
    private static Dictionary<string, string> ParseFrontmatter(string content)
    {
        var meta = new Dictionary<string, string>();
        if (!content.StartsWith("---")) return meta;

        var end = content.IndexOf("---", 3);
        if (end < 0) return meta;

        var frontmatter = content[3..end];
        foreach (var line in frontmatter.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key   = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
                meta[key] = value;
        }
        return meta;
    }

    // ─── Private: render a single line with inline styles ───
    private static void RenderLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            AnsiConsole.WriteLine();
            return;
        }

        // Code block fences — just show as grey
        if (line.TrimStart().StartsWith("```"))
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
            return;
        }

        // Bullet
        if (Regex.IsMatch(line, @"^\s*[-*]\s"))
        {
            var text = Regex.Replace(line, @"^\s*[-*]\s", "");
            AnsiConsole.MarkupLine($"  [green]•[/] {Markup.Escape(ApplyInline(text))}");
            return;
        }

        // Numbered list
        if (Regex.IsMatch(line, @"^\s*\d+\.\s"))
        {
            var m = Regex.Match(line, @"^\s*(\d+)\.\s(.+)");
            AnsiConsole.MarkupLine(
                $"  [cyan]{m.Groups[1].Value}.[/] {Markup.Escape(ApplyInline(m.Groups[2].Value))}");
            return;
        }

        // Blockquote
        if (line.TrimStart().StartsWith(">"))
        {
            var text = line.TrimStart()[1..].Trim();
            AnsiConsole.MarkupLine($"[grey] ▌ {Markup.Escape(text)}[/]");
            return;
        }

        AnsiConsole.MarkupLine(Markup.Escape(ApplyInline(line)));
    }

    private static string ApplyInline(string text)
    {
        // Strip bold/italic/code markers for plain display
        // (Spectre.Console doesn't support nested markup easily)
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = Regex.Replace(text, @"\*(.+?)\*",     "$1");
        text = Regex.Replace(text, @"`([^`]+)`",     "[$1]");
        return text;
    }
}

// ─────────────────────────────────────────────────────────────
//  Data models
// ─────────────────────────────────────────────────────────────
public class MarkdownFile
{
    public string                       Path        { get; set; } = "";
    public string                       FileName    { get; set; } = "";
    public string                       RawContent  { get; set; } = "";
    public List<MarkdownSection>        Sections    { get; set; } = [];
    public Dictionary<string, string>   Metadata    { get; set; } = [];
    public int                          WordCount   { get; set; }
    public bool                         IsSkillFile { get; set; }
}

public class MarkdownSection
{
    public int    Level { get; set; }
    public string Title { get; set; } = "";
    public string Body  { get; set; } = "";
}
