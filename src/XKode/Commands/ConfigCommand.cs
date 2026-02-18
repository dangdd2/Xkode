using XKode.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XKode.Commands;

// ─────────────────────────────────────────────────────────────
//  ConfigCommand — View and initialize global config
//  Usage:
//    xkode config       → Show current config
//    xkode config init  → Create default config file
//    xkode config env   → Show environment variable help
// ─────────────────────────────────────────────────────────────
public class ConfigCommand : Command<ConfigCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[action]")]
        public string? Action { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var action = settings.Action?.ToLower() ?? "show";

        switch (action)
        {
            case "show":
                ShowConfig();
                break;

            case "init":
                InitConfig();
                break;

            case "env":
                ConfigService.PrintEnvHelp();
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown action:[/] {settings.Action}");
                AnsiConsole.MarkupLine("[grey]Usage: xkode config [show|init|env][/]");
                return 1;
        }

        return 0;
    }

    private static void ShowConfig()
    {
        var config = ConfigService.Load();
        config.Print();
        AnsiConsole.MarkupLine($"\n[grey dim]Edit config: ~/.config/xkode/config.json[/]");
        AnsiConsole.MarkupLine($"[grey dim]Init config: xkode config init[/]");
        AnsiConsole.MarkupLine($"[grey dim]Env vars:    xkode config env[/]");
    }

    private static void InitConfig()
    {
        ConfigService.InitializeIfMissing();
        var config = ConfigService.Load();
        config.Print();
    }
}
