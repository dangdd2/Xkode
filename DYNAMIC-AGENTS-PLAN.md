# Dynamic Agent System Implementation Plan

**Version:** 1.0  
**Created:** 2026-02-24  
**Target Version:** XKode v0.3.0  

---

## Executive Summary

Transform XKode from hardcoded agents to a dynamic, file-based agent system similar to Claude Code. This enables users to create, customize, and share agents without recompiling code.

### Goals

1. **Flexibility**: Users can create custom agents via markdown files
2. **Extensibility**: No code changes needed to add new agents
3. **Shareability**: Agents can be version-controlled and shared
4. **Compatibility**: Backward compatible with existing workflows

### Architecture

```
.xkode/
├── agents/                    ← User custom agents
│   ├── frontend-expert.md
│   ├── backend-expert.md
│   └── custom-agent.md
└── SKILL.md

Built-in agents (embedded in app or ~/.xkode/agents/builtin/):
├── planner.md
├── executor.md
└── reviewer.md
```

---

## Phase 1: Core Infrastructure

**Duration:** 2-3 days  
**Goal:** Create foundation for loading and parsing agent definitions

### 1.1 Agent Definition Model

**File:** `src/XKode/Models/AgentDefinition.cs`

```csharp
using System.Text.Json.Serialization;

namespace XKode.Models;

/// <summary>
/// Represents a dynamic agent loaded from a markdown file
/// </summary>
public class AgentDefinition
{
    // ─── Frontmatter Properties (YAML) ────────────────────
    
    /// <summary>
    /// Unique identifier for the agent (kebab-case)
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Description of when to use this agent (with examples)
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>
    /// List of tools this agent can use
    /// </summary>
    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();

    /// <summary>
    /// Model to use (sonnet, opus, haiku, or specific model name)
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "qwen2.5-coder:7b";

    /// <summary>
    /// UI color for this agent (for display purposes)
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    // ─── Additional Properties ────────────────────────────
    
    /// <summary>
    /// Temperature for generation (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Max tokens for response
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Tags for categorization
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    // ─── Body Content ──────────────────────────────────────
    
    /// <summary>
    /// System prompt (markdown body after frontmatter)
    /// </summary>
    public string SystemPrompt { get; set; } = "";

    // ─── Metadata ──────────────────────────────────────────
    
    /// <summary>
    /// Path to the agent definition file
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Whether this is a built-in agent
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// When this agent was loaded
    /// </summary>
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;

    // ─── Helper Methods ────────────────────────────────────
    
    /// <summary>
    /// Get display name for UI
    /// </summary>
    public string DisplayName => Name.Replace("-", " ")
        .Split(' ')
        .Select(w => char.ToUpper(w[0]) + w[1..])
        .Aggregate((a, b) => a + " " + b);

    /// <summary>
    /// Check if agent has specific tool
    /// </summary>
    public bool HasTool(string tool) => 
        Tools.Contains(tool, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Check if agent matches tags
    /// </summary>
    public bool HasTag(string tag) => 
        Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
}
```

---

### 1.2 Agent Loader Service

**File:** `src/XKode/Services/AgentLoaderService.cs`

```csharp
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XKode.Services;

/// <summary>
/// Loads and manages dynamic agent definitions from markdown files
/// </summary>
public class AgentLoaderService
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly Dictionary<string, AgentDefinition> _agents = new();
    private readonly List<string> _searchPaths = new();

    public AgentLoaderService()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        InitializeSearchPaths();
    }

    // ─── Initialization ────────────────────────────────────
    
    private void InitializeSearchPaths()
    {
        // User agents (project-specific)
        _searchPaths.Add(Path.Combine(Directory.GetCurrentDirectory(), ".xkode", "agents"));

        // User agents (global)
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _searchPaths.Add(Path.Combine(homeDir, ".xkode", "agents"));

        // Built-in agents
        var appDir = AppContext.BaseDirectory;
        _searchPaths.Add(Path.Combine(appDir, "agents", "builtin"));
    }

    // ─── Loading Methods ───────────────────────────────────
    
    /// <summary>
    /// Load all agents from search paths
    /// </summary>
    public void LoadAllAgents()
    {
        _agents.Clear();

        foreach (var searchPath in _searchPaths)
        {
            if (!Directory.Exists(searchPath))
                continue;

            var mdFiles = Directory.GetFiles(searchPath, "*.md", SearchOption.TopDirectoryOnly);
            
            foreach (var file in mdFiles)
            {
                try
                {
                    var agent = LoadAgentFromFile(file);
                    if (agent != null)
                    {
                        _agents[agent.Name] = agent;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load agent from {file}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Load single agent from markdown file
    /// </summary>
    private AgentDefinition? LoadAgentFromFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        
        // Extract YAML frontmatter
        var frontmatterMatch = Regex.Match(content, @"^---\s*\n(.*?)\n---\s*\n(.*)$", 
            RegexOptions.Singleline);

        if (!frontmatterMatch.Success)
        {
            throw new InvalidOperationException($"Invalid agent file format (missing frontmatter): {filePath}");
        }

        var yamlContent = frontmatterMatch.Groups[1].Value;
        var markdownBody = frontmatterMatch.Groups[2].Value.Trim();

        // Parse YAML frontmatter
        var agent = _yamlDeserializer.Deserialize<AgentDefinition>(yamlContent);
        
        // Set body and metadata
        agent.SystemPrompt = markdownBody;
        agent.FilePath = filePath;
        agent.IsBuiltIn = filePath.Contains("builtin");

        // Validate
        if (string.IsNullOrWhiteSpace(agent.Name))
            throw new InvalidOperationException($"Agent must have a name: {filePath}");

        return agent;
    }

    // ─── Query Methods ─────────────────────────────────────
    
    /// <summary>
    /// Get agent by name
    /// </summary>
    public AgentDefinition? GetAgent(string name)
    {
        return _agents.GetValueOrDefault(name);
    }

    /// <summary>
    /// Get all loaded agents
    /// </summary>
    public List<AgentDefinition> GetAllAgents()
    {
        return _agents.Values.OrderBy(a => a.Name).ToList();
    }

    /// <summary>
    /// Get agents by tag
    /// </summary>
    public List<AgentDefinition> GetAgentsByTag(string tag)
    {
        return _agents.Values
            .Where(a => a.HasTag(tag))
            .OrderBy(a => a.Name)
            .ToList();
    }

    /// <summary>
    /// Search agents by description
    /// </summary>
    public List<AgentDefinition> SearchAgents(string query)
    {
        return _agents.Values
            .Where(a => 
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Name)
            .ToList();
    }

    // ─── Management Methods ────────────────────────────────
    
    /// <summary>
    /// Reload all agents (useful after user creates new agent)
    /// </summary>
    public void ReloadAgents()
    {
        LoadAllAgents();
    }

    /// <summary>
    /// Check if agent exists
    /// </summary>
    public bool AgentExists(string name)
    {
        return _agents.ContainsKey(name);
    }

    /// <summary>
    /// Get count of loaded agents
    /// </summary>
    public int AgentCount => _agents.Count;

    /// <summary>
    /// Get built-in agent names
    /// </summary>
    public List<string> GetBuiltInAgentNames()
    {
        return _agents.Values
            .Where(a => a.IsBuiltIn)
            .Select(a => a.Name)
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Get user agent names
    /// </summary>
    public List<string> GetUserAgentNames()
    {
        return _agents.Values
            .Where(a => !a.IsBuiltIn)
            .Select(a => a.Name)
            .OrderBy(n => n)
            .ToList();
    }
}
```

**Dependencies to add:**
```xml
<PackageReference Include="YamlDotNet" Version="15.1.0" />
```

---

### 1.3 Dynamic Agent Class

**File:** `src/XKode/Agents/DynamicAgent.cs`

```csharp
using XKode.Models;
using XKode.Services;

namespace XKode.Agents;

/// <summary>
/// Agent instance created from a dynamic agent definition
/// </summary>
public class DynamicAgent : AgentBase
{
    private readonly AgentDefinition _definition;

    public DynamicAgent(
        AgentDefinition definition, 
        OllamaService ollama, 
        ConfigService config) 
        : base(ollama, definition.Name, definition.Model)
    {
        _definition = definition;
    }

    /// <summary>
    /// System prompt from agent definition
    /// </summary>
    public override string SystemPrompt => _definition.SystemPrompt;

    /// <summary>
    /// Get the agent definition
    /// </summary>
    public AgentDefinition Definition => _definition;

    /// <summary>
    /// Execute with custom temperature
    /// </summary>
    public async Task<string> ExecuteAsync(
        string input, 
        CancellationToken ct = default)
    {
        // Future: Set temperature from definition
        // For now, use default implementation
        return await base.ExecuteAsync(input, ct);
    }
}
```

---

### 1.4 Tests

**File:** `tests/XKode.Tests/AgentLoaderServiceTests.cs`

```csharp
using Xunit;
using FluentAssertions;

namespace XKode.Tests;

public class AgentLoaderServiceTests
{
    [Fact]
    public void LoadAgentFromFile_ValidFile_LoadsCorrectly()
    {
        // Test loading valid agent markdown file
    }

    [Fact]
    public void LoadAgentFromFile_MissingFrontmatter_ThrowsException()
    {
        // Test error handling
    }

    [Fact]
    public void GetAgent_ExistingAgent_ReturnsAgent()
    {
        // Test retrieval
    }

    [Fact]
    public void SearchAgents_WithQuery_ReturnsMatchingAgents()
    {
        // Test search
    }
}
```

---

### 1.5 Deliverables

- ✅ `AgentDefinition` model
- ✅ `AgentLoaderService` with YAML parsing
- ✅ `DynamicAgent` class
- ✅ Unit tests
- ✅ YamlDotNet dependency added

---

## Phase 2: Migration & Integration

**Duration:** 2-3 days  
**Goal:** Convert existing agents to markdown files and integrate loader

### 2.1 Convert Built-In Agents to Markdown

**Directory:** `src/XKode/agents/builtin/`

Create these files:

#### planner.md
```markdown
---
name: planner
description: Strategic planning agent that breaks down tasks into executable steps. Use for complex multi-step features, refactoring projects, or any task requiring structured planning.
tools: []
model: qwen2.5-coder:32b
color: blue
tags: [planning, architecture, breakdown]
temperature: 0.7
max_tokens: 4000
---

You are a planning agent specialized in breaking down software development tasks.

CRITICAL JSON RULES:
1. Output ONLY valid JSON - no explanation, no markdown, no thinking
2. Start your response IMMEDIATELY with { (the opening brace)
3. ALL strings must be properly escaped:
   - Use \\n for newlines (NEVER actual line breaks in strings)
   - Use \\" for quotes within strings
   - Use \\\\ for backslashes
4. Keep all text SHORT and simple (under 200 chars per field)
5. Test your JSON is valid before outputting

[... rest of existing PlannerAgent system prompt ...]
```

#### executor.md
```markdown
---
name: executor
description: Implementation agent that executes individual steps from a plan. Use after planner creates a plan, or for single-step code implementation tasks.
tools: []
model: qwen2.5-coder:7b
color: green
tags: [implementation, coding, execution]
temperature: 0.7
max_tokens: 4000
---

You are an implementation agent specialized in executing code changes.

[... rest of existing ExecutorAgent system prompt ...]
```

#### reviewer.md
```markdown
---
name: reviewer
description: Code review agent that finds bugs, security issues, and suggests improvements. Use after code implementation to ensure quality.
tools: []
model: qwen2.5-coder:32b
color: yellow
tags: [review, quality, security]
temperature: 0.7
max_tokens: 4000
---

You are a code review agent specialized in finding issues and suggesting improvements.

[... rest of existing ReviewerAgent system prompt ...]
```

---

### 2.2 Update AgentOrchestrator

**File:** `src/XKode/Agents/AgentOrchestrator.cs`

```csharp
// BEFORE:
public class AgentOrchestrator(
    PlannerAgent planner,
    ExecutorAgent executor,
    ReviewerAgent reviewer,
    FileService fileService,
    ConfigService config)

// AFTER:
public class AgentOrchestrator(
    AgentLoaderService agentLoader,
    OllamaService ollama,
    CodeIndexService codeIndex,
    FileService fileService,
    ConfigService config)
{
    private DynamicAgent? _planner;
    private DynamicAgent? _executor;
    private DynamicAgent? _reviewer;

    // Lazy load agents
    private DynamicAgent GetPlanner()
    {
        if (_planner == null)
        {
            var def = agentLoader.GetAgent("planner") 
                ?? throw new Exception("Planner agent not found");
            _planner = new DynamicAgent(def, ollama, config);
        }
        return _planner;
    }

    // Similar for executor and reviewer...
}
```

---

### 2.3 Update AgentCommand

**File:** `src/XKode/Commands/AgentCommand.cs`

Add agent selection:

```csharp
[CommandOption("--agent <NAME>")]
[Description("Specify which agent to use (default: auto-select)")]
public string? AgentName { get; set; }

public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
{
    // Load agents
    var agentLoader = new AgentLoaderService();
    agentLoader.LoadAllAgents();

    // If agent specified, use it
    if (!string.IsNullOrWhiteSpace(settings.AgentName))
    {
        var agentDef = agentLoader.GetAgent(settings.AgentName);
        if (agentDef == null)
        {
            AnsiConsole.MarkupLine($"[red]Agent '{settings.AgentName}' not found[/]");
            return 1;
        }

        var agent = new DynamicAgent(agentDef, ollama, config);
        // Use this agent for execution
    }
    else
    {
        // Default multi-agent workflow
        var orchestrator = new AgentOrchestrator(agentLoader, ...);
    }
}
```

---

### 2.4 Update Program.cs

**File:** `src/XKode/Program.cs`

```csharp
// Register agent loader as singleton
builder.Services.AddSingleton<AgentLoaderService>();

// Remove hardcoded agent registrations
// builder.Services.AddScoped<PlannerAgent>();  ← DELETE
// builder.Services.AddScoped<ExecutorAgent>(); ← DELETE
// builder.Services.AddScoped<ReviewerAgent>(); ← DELETE

// Initialize agent loader at startup
var agentLoader = app.Services.GetRequiredService<AgentLoaderService>();
agentLoader.LoadAllAgents();
AnsiConsole.MarkupLine($"[grey]Loaded {agentLoader.AgentCount} agents[/]");
```

---

### 2.5 Deliverables

- ✅ 3 built-in agent markdown files
- ✅ Updated `AgentOrchestrator` to use dynamic agents
- ✅ Updated `AgentCommand` with --agent option
- ✅ Updated `Program.cs` DI registration
- ✅ Backward compatibility maintained

---

## Phase 3: Agent Management & UX

**Duration:** 1-2 days  
**Goal:** Add commands for managing agents and improve user experience

### 3.1 Agents Command

**File:** `src/XKode/Commands/AgentsCommand.cs`

```csharp
using Spectre.Console;
using Spectre.Console.Cli;

namespace XKode.Commands;

public class AgentsCommand : Command<AgentsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[action]")]
        [Description("Action: list, create, info, reload")]
        public string? Action { get; set; }

        [CommandArgument(1, "[name]")]
        [Description("Agent name (for info action)")]
        public string? Name { get; set; }
    }

    private readonly AgentLoaderService _agentLoader;

    public AgentsCommand(AgentLoaderService agentLoader)
    {
        _agentLoader = agentLoader;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var action = settings.Action?.ToLower() ?? "list";

        return action switch
        {
            "list" => ListAgents(),
            "create" => CreateAgent(),
            "info" => ShowAgentInfo(settings.Name),
            "reload" => ReloadAgents(),
            _ => ShowHelp()
        };
    }

    // ─── List Agents ───────────────────────────────────────
    
    private int ListAgents()
    {
        var agents = _agentLoader.GetAllAgents();

        if (agents.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No agents found[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]Description[/]")
            .AddColumn("[bold]Model[/]")
            .AddColumn("[bold]Type[/]");

        foreach (var agent in agents)
        {
            var desc = agent.Description.Length > 60 
                ? agent.Description[..60] + "..." 
                : agent.Description;
            
            var type = agent.IsBuiltIn ? "[grey]built-in[/]" : "[cyan]custom[/]";

            table.AddRow(
                $"[cyan]{agent.Name}[/]",
                Markup.Escape(desc),
                $"[grey]{agent.Model}[/]",
                type
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[grey]Total: {agents.Count} agents[/]");

        return 0;
    }

    // ─── Create Agent ──────────────────────────────────────
    
    private int CreateAgent()
    {
        AnsiConsole.MarkupLine("[bold cyan]Create New Agent[/]\n");

        // Interactive prompts
        var name = AnsiConsole.Ask<string>("Agent [cyan]name[/] (kebab-case):");
        var description = AnsiConsole.Ask<string>("Short [cyan]description[/]:");
        var model = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [cyan]model[/]:")
                .AddChoices("qwen2.5-coder:7b", "qwen2.5-coder:32b", "custom"));

        if (model == "custom")
        {
            model = AnsiConsole.Ask<string>("Enter model name:");
        }

        // Create template
        var template = CreateAgentTemplate(name, description, model);
        
        // Save to .xkode/agents/
        var agentsDir = Path.Combine(Directory.GetCurrentDirectory(), ".xkode", "agents");
        Directory.CreateDirectory(agentsDir);
        
        var filePath = Path.Combine(agentsDir, $"{name}.md");
        File.WriteAllText(filePath, template);

        AnsiConsole.MarkupLine($"\n[green]✓ Agent created:[/] {filePath}");
        AnsiConsole.MarkupLine("[grey]Edit the file to customize the system prompt[/]");

        return 0;
    }

    // ─── Agent Info ────────────────────────────────────────
    
    private int ShowAgentInfo(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            AnsiConsole.MarkupLine("[red]Error: Agent name required[/]");
            AnsiConsole.MarkupLine("[grey]Usage: xkode agents info <name>[/]");
            return 1;
        }

        var agent = _agentLoader.GetAgent(name);
        if (agent == null)
        {
            AnsiConsole.MarkupLine($"[red]Agent '{name}' not found[/]");
            return 1;
        }

        // Display agent details
        var panel = new Panel(
            new Markup($"""
                [bold]Name:[/] {agent.Name}
                [bold]Description:[/] {Markup.Escape(agent.Description)}
                [bold]Model:[/] {agent.Model}
                [bold]Temperature:[/] {agent.Temperature}
                [bold]Max Tokens:[/] {agent.MaxTokens}
                [bold]Tools:[/] {string.Join(", ", agent.Tools)}
                [bold]Tags:[/] {string.Join(", ", agent.Tags)}
                [bold]Type:[/] {(agent.IsBuiltIn ? "Built-in" : "Custom")}
                [bold]File:[/] {agent.FilePath}
                """))
            .Header($"[cyan]{agent.DisplayName}[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);

        return 0;
    }

    // ─── Reload Agents ─────────────────────────────────────
    
    private int ReloadAgents()
    {
        _agentLoader.ReloadAgents();
        AnsiConsole.MarkupLine($"[green]✓ Reloaded {_agentLoader.AgentCount} agents[/]");
        return 0;
    }

    // ─── Helper Methods ────────────────────────────────────
    
    private static string CreateAgentTemplate(string name, string description, string model)
    {
        return $"""
            ---
            name: {name}
            description: {description}
            tools: []
            model: {model}
            color: blue
            tags: []
            temperature: 0.7
            max_tokens: 4000
            ---

            You are a specialized agent for [DESCRIBE PURPOSE].

            ## Your Responsibilities

            1. [Responsibility 1]
            2. [Responsibility 2]
            3. [Responsibility 3]

            ## Guidelines

            - [Guideline 1]
            - [Guideline 2]
            - [Guideline 3]

            ## Output Format

            [Describe expected output format]

            ## Examples

            [Provide examples of inputs and outputs]
            """;
    }

    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("""
            [bold]XKode Agents[/]

            [cyan]Usage:[/]
              xkode agents [action] [name]

            [cyan]Actions:[/]
              list           List all available agents
              create         Create a new agent interactively
              info <name>    Show details about an agent
              reload         Reload all agents from disk

            [cyan]Examples:[/]
              xkode agents list
              xkode agents create
              xkode agents info planner
              xkode agents reload
            """);

        return 0;
    }
}
```

---

### 3.2 Register Command

**File:** `src/XKode/Program.cs`

```csharp
config.AddCommand<AgentsCommand>("agents")
    .WithDescription("Manage dynamic agents")
    .WithExample("agents", "list")
    .WithExample("agents", "create")
    .WithExample("agents", "info planner");
```

---

### 3.3 Auto-Select Agent

**File:** `src/XKode/Services/AgentSelectorService.cs`

```csharp
namespace XKode.Services;

/// <summary>
/// Automatically select the best agent for a task
/// </summary>
public class AgentSelectorService
{
    private readonly AgentLoaderService _agentLoader;

    public AgentSelectorService(AgentLoaderService agentLoader)
    {
        _agentLoader = agentLoader;
    }

    /// <summary>
    /// Select agent based on task description
    /// </summary>
    public AgentDefinition? SelectAgent(string taskDescription)
    {
        var lower = taskDescription.ToLower();

        // Keywords matching
        var keywords = new Dictionary<string, string[]>
        {
            ["planner"] = new[] { "plan", "break down", "steps", "architecture" },
            ["reviewer"] = new[] { "review", "check", "quality", "bugs", "issues" },
            ["executor"] = new[] { "implement", "code", "create", "build" }
        };

        foreach (var (agentName, words) in keywords)
        {
            if (words.Any(w => lower.Contains(w)))
            {
                return _agentLoader.GetAgent(agentName);
            }
        }

        // Default to planner for complex tasks
        return _agentLoader.GetAgent("planner");
    }
}
```

---

### 3.4 Deliverables

- ✅ `AgentsCommand` for management
- ✅ `AgentSelectorService` for auto-selection
- ✅ Interactive agent creation
- ✅ Agent info display
- ✅ Reload functionality

---

## Testing Strategy

### Unit Tests

- ✅ YAML parsing edge cases
- ✅ Agent loading from various locations
- ✅ Search and filtering
- ✅ Error handling

### Integration Tests

- ✅ Load built-in agents
- ✅ Load custom agents
- ✅ Execute dynamic agent
- ✅ Multi-agent workflow with dynamic agents

### Manual Testing

- ✅ Create custom agent via CLI
- ✅ Use custom agent in workflow
- ✅ Share agent file with team
- ✅ Reload after editing agent

---

## Migration Path

### For Users

**Step 1:** Update XKode
```bash
dotnet tool update --global XKode
```

**Step 2:** No action needed - built-in agents work automatically

**Step 3 (Optional):** Create custom agents
```bash
xkode agents create
```

### Backward Compatibility

- ✅ Existing `xkode agent "task"` commands work unchanged
- ✅ No breaking changes to CLI
- ✅ Old workflows continue to function

---

## Documentation Updates

### README.md

Add section:
```markdown
## Custom Agents

Create your own specialized agents:

\`\`\`bash
xkode agents create
\`\`\`

List available agents:
\`\`\`bash
xkode agents list
\`\`\`

Use a specific agent:
\`\`\`bash
xkode agent "task" --agent frontend-expert
\`\`\`
```

### AGENTS.md (New)

Complete guide on:
- Creating custom agents
- Agent file format
- Best practices
- Examples
- Sharing agents

---

## Future Enhancements

### Phase 4 (Future)

- **Agent Marketplace**: Download community agents
- **Agent Templates**: Pre-built templates for common scenarios
- **Agent Chaining**: Define workflows in agent files
- **Agent Variables**: Parameterized prompts
- **Agent Versioning**: Track agent versions
- **Agent Analytics**: Usage statistics

---

## Success Criteria

### Phase 1
- ✅ Agents load from markdown files
- ✅ YAML frontmatter parsed correctly
- ✅ System prompt extracted
- ✅ Tests pass

### Phase 2
- ✅ Built-in agents converted
- ✅ Existing workflows work
- ✅ No breaking changes
- ✅ Performance unchanged

### Phase 3
- ✅ Users can create custom agents
- ✅ Agents can be listed/managed
- ✅ Auto-selection works
- ✅ Documentation complete

---

## Timeline

**Total Duration:** 5-8 days

- **Phase 1:** Days 1-3 (Core infrastructure)
- **Phase 2:** Days 4-6 (Migration & integration)
- **Phase 3:** Days 7-8 (Management & UX)

**Milestone Releases:**
- v0.3.0-alpha: Phase 1 complete
- v0.3.0-beta: Phase 2 complete
- v0.3.0: Phase 3 complete (production ready)

---

## Risks & Mitigation

### Risk 1: YAML Parsing Issues
**Mitigation:** Extensive testing, clear error messages, validation

### Risk 2: Performance Impact
**Mitigation:** Cache loaded agents, lazy loading

### Risk 3: User Confusion
**Mitigation:** Clear documentation, examples, interactive creation

### Risk 4: Breaking Changes
**Mitigation:** Maintain backward compatibility, gradual migration

---

## Conclusion

This plan transforms XKode into a flexible, extensible platform where users can create and share specialized agents without touching code. The three-phase approach ensures stability while delivering incremental value.

**Next Steps:**
1. Review and approve plan
2. Begin Phase 1 implementation
3. Create prototype with one dynamic agent
4. Gather feedback
5. Continue with full implementation
