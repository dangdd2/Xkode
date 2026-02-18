using XKode;
using XKode.Commands;
using XKode.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

// ─────────────────────────────────────────────────────────────
//  OllamaCode CLI — Entry Point
//  Local AI Coding Agent powered by Ollama
// ─────────────────────────────────────────────────────────────

var services = new ServiceCollection();
services.AddHttpClient<OllamaService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(10);
});
// OllamaService is already registered (Transient) by AddHttpClient above.
// Do NOT re-register as Singleton — that would create a second instance
// with a plain HttpClient missing the BaseAddress.
services.AddSingleton<FileService>();
services.AddSingleton<CodeIndexService>();
services.AddSingleton<ShellService>();
services.AddSingleton<MarkdownService>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("xkode");
    config.SetApplicationVersion("0.1.0");
    config.CaseSensitivity(CaseSensitivity.None);

    config.AddCommand<ChatCommand>("chat")
          .WithDescription("Start an interactive AI chat session with your codebase")
          .WithExample("chat")
          .WithExample("chat", "--model", "qwen2.5-coder:7b")
          .WithExample("chat", "--path", "/my/project");

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

    // Show banner if no args
    if (args.Length == 0)
    {
        Banner.Show();
        config.AddCommand<ChatCommand>("chat");
    }
});

return await app.RunAsync(args.Length == 0 ? ["chat"] : args);
