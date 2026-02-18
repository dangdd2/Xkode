using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XKode.Services;

// ─────────────────────────────────────────────────────────────
//  ConfigService — Global settings stored in ~/.config/xkode/
//
//  Reads from (in order of priority):
//    1. Environment variables (XKODE_*)
//    2. User config file (~/.config/xkode/config.json)
//    3. Default values
//
//  Usage:
//    var config = ConfigService.Load();
//    config.OllamaUrl         // "http://localhost:11434"
//    config.DefaultModel      // "qwen2.5-coder:7b"
//    config.Save();           // Persist changes
// ─────────────────────────────────────────────────────────────
public class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "xkode");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    // ── Configuration properties ────────────────────────────
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string DefaultModel { get; set; } = "qwen2.5-coder:7b";
    public int MaxContextFiles { get; set; } = 80;
    public bool AutoAccept { get; set; } = false;
    public bool AutoLoadSkill { get; set; } = true;
    public int HistoryLimit { get; set; } = 100;
    public string Theme { get; set; } = "dark";

    // Advanced
    public int OllamaTimeoutSeconds { get; set; } = 600;
    public int HealthCheckTimeoutSeconds { get; set; } = 3;

    // ── Load config (env vars > file > defaults) ────────────
    public static ConfigService Load()
    {
        var config = LoadFromFile() ?? new ConfigService();

        // Override with environment variables (highest priority)
        config.OllamaUrl = Environment.GetEnvironmentVariable("XKODE_OLLAMA_URL")
            ?? Environment.GetEnvironmentVariable("OLLAMA_HOST")
            ?? config.OllamaUrl;

        config.DefaultModel = Environment.GetEnvironmentVariable("XKODE_MODEL")
            ?? config.DefaultModel;

        if (int.TryParse(Environment.GetEnvironmentVariable("XKODE_MAX_FILES"), out var maxFiles))
            config.MaxContextFiles = maxFiles;

        if (bool.TryParse(Environment.GetEnvironmentVariable("XKODE_AUTO_ACCEPT"), out var autoAccept))
            config.AutoAccept = autoAccept;

        if (bool.TryParse(Environment.GetEnvironmentVariable("XKODE_AUTO_SKILL"), out var autoSkill))
            config.AutoLoadSkill = autoSkill;

        return config;
    }

    // ── Load from JSON file ──────────────────────────────────
    private static ConfigService? LoadFromFile()
    {
        if (!File.Exists(ConfigPath)) return null;

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<ConfigService>(json);
        }
        catch
        {
            // Invalid JSON — ignore and use defaults
            return null;
        }
    }

    // ── Save config to file ──────────────────────────────────
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not save config: {ex.Message}");
        }
    }

    // ── Generate example config file ─────────────────────────
    public static string GenerateExampleConfig()
    {
        var example = new ConfigService();
        return JsonSerializer.Serialize(example, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    // ── Print current config to console ──────────────────────
    public void Print()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Current Configuration:");
        sb.AppendLine($"  Config file: {ConfigPath}");
        sb.AppendLine($"  Ollama URL: {OllamaUrl}");
        sb.AppendLine($"  Default model: {DefaultModel}");
        sb.AppendLine($"  Max context files: {MaxContextFiles}");
        sb.AppendLine($"  Auto-accept changes: {AutoAccept}");
        sb.AppendLine($"  Auto-load SKILL.md: {AutoLoadSkill}");
        sb.AppendLine($"  History limit: {HistoryLimit}");
        sb.AppendLine($"  Theme: {Theme}");
        sb.AppendLine($"  Timeouts: {HealthCheckTimeoutSeconds}s health, {OllamaTimeoutSeconds}s completion");
        Console.WriteLine(sb.ToString());
    }

    // ── Initialize default config file if missing ────────────
    public static void InitializeIfMissing()
    {
        if (File.Exists(ConfigPath)) return;

        Directory.CreateDirectory(ConfigDir);
        var config = new ConfigService();
        config.Save();

        Console.WriteLine($"✓ Created default config at: {ConfigPath}");
    }

    // ── Environment variable help ────────────────────────────
    public static void PrintEnvHelp()
    {
        Console.WriteLine(@"
Environment Variables (override config file):

  XKODE_OLLAMA_URL         Ollama server URL (default: http://localhost:11434)
  OLLAMA_HOST              Alternative to XKODE_OLLAMA_URL (Ollama standard)
  XKODE_MODEL              Default model (default: qwen2.5-coder:7b)
  XKODE_MAX_FILES          Max files to index (default: 80)
  XKODE_AUTO_ACCEPT        Auto-accept file changes: true/false
  XKODE_AUTO_SKILL         Auto-load SKILL.md: true/false

Examples:
  export XKODE_OLLAMA_URL=http://192.168.1.5:11434
  export XKODE_MODEL=qwen2.5-coder:32b
  XKODE_AUTO_ACCEPT=true xkode run 'fix all bugs'
");
    }
}
