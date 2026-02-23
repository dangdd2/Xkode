using XKode;
using XKode.Commands;
using XKode.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

// ─────────────────────────────────────────────────────────────
//  OllamaCode CLI — Entry Point
//  Local AI Coding Agent powered by Ollama
// ─────────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────────
//  XKode CLI — Entry Point
// ─────────────────────────────────────────────────────────────

// Load global config
var config = ConfigService.Load();

var services = new ServiceCollection();
services.AddSingleton(config); // Register config for DI

services.AddHttpClient<OllamaService>(client =>
{
    client.BaseAddress = new Uri(config.OllamaUrl);
    client.Timeout = TimeSpan.FromSeconds(config.OllamaTimeoutSeconds);
});
// OllamaService is already registered (Transient) by AddHttpClient above.
// Do NOT re-register as Singleton — that would create a second instance
// with a plain HttpClient missing the BaseAddress.
services.AddSingleton<FileService>();
services.AddSingleton<CodeIndexService>();
services.AddSingleton<ShellService>();
services.AddSingleton<MarkdownService>();

// Multi-agent services
services.AddSingleton<XKode.Agents.PlannerAgent>();
services.AddSingleton<XKode.Agents.ExecutorAgent>();
services.AddSingleton<XKode.Agents.ReviewerAgent>();
services.AddSingleton<XKode.Agents.AgentOrchestrator>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("xkode");
    config.SetApplicationVersion("0.2.0");
    config.CaseSensitivity(CaseSensitivity.None);

    config.AddCommand<ChatCommand>("chat")
          .WithDescription("Start an interactive AI chat session with your codebase")
          .WithExample("chat")
          .WithExample("chat", "--model", ConfigService.DefaultModelName)
          .WithExample("chat", "--path", "/my/project");

    config.AddCommand<AgentCommand>("agent")
          .WithDescription("Multi-agent mode: Plan → Execute → Review")
          .WithExample("agent", "\"Add authentication to my app\"")
          .WithExample("agent", "\"Refactor Services folder\"", "--path", "./src")
          .WithExample("agent", "\"Write unit tests\"", "--yes");

    config.AddCommand<RunCommand>("run")
          .WithDescription("Run a single AI task (non-interactive)")
          .WithExample("run", "\"Explain this codebase\"")
          .WithExample("run", "\"Add error handling to Program.cs\"");

    config.AddCommand<ReviewCommand>("review")
          .WithDescription("AI code review for a file or the whole project")
          .WithExample("review", "src/MyClass.cs")
          .WithExample("review", "--path", ".");

    config.AddCommand<ModelsCommand>("models")
          .WithDescription("List available Ollama models");

    config.AddCommand<ConfigCommand>("config")
          .WithDescription("View or initialize configuration")
          .WithExample("config")
          .WithExample("config", "init")
          .WithExample("config", "env");
});

string defaultCommand = "chat";
#if DEBUG
if (args.Length == 0)
{
    // execute MODE
    args =
    [
        "agent",
        "Add C# code that demo paypal payment with all common API: make , void payment",      // Single element
        "--path", "C:\\Work\\Lab\\test\\PaypalPayment",     // Separate elements
        "--yes",
        "--no-review"
    ];

    // export to Mark down file Mode
    //args =
    //[
    //    "agent",
    //    "create an AI Stock based on the news inputs, system can summarize and make a decision we should BUY or SELL",      // Single element
    //    "--path", "C:\\Work\\Lab\\test\\vue",     // Separate elements
    //    "--export-plan", // Save the plan to a file
    //    "--yes", // auto - approve all steps without prompting
    //    "--no-review" // skip the review phase (useful for testing just the planner and executor)
    //];
    //args =
    //[
    //    "agent",
    //    string.Empty,      // Single element
    //    "--path", "C:\\Work\\Lab\\test\\csharp",     // Separate elements
    //    "--yes",
    //    "--no-review"
    //];

}
#endif
return await app.RunAsync(args.Length == 0 ? [defaultCommand] : args);

