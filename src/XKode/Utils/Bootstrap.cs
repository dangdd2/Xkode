using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.DependencyInjection;
using XKode.Services;

namespace XKode;

// ─────────────────────────────────────────────────────────────
//  ASCII Banner
// ─────────────────────────────────────────────────────────────
public static class Banner
{
    public static void Show()
    {
        AnsiConsole.Write(new FigletText("XKode")
            .Centered()
            .Color(Color.DeepSkyBlue1));

        AnsiConsole.Write(
            new Panel("[bold white]Local AI Coding Agent — Powered by Ollama[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.DeepSkyBlue1)
                .Padding(1, 0));

        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine($"  [grey]Version:[/] [white]0.1.0[/]   [grey]|[/]   [grey]Model:[/] [green]{ConfigService.DefaultModelName}[/]   [grey]|[/]   [grey]Privacy:[/] [bold green]100% Local[/]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("  [grey dim]Type [bold white]/help[/] for commands, [bold white]/exit[/] to quit[/]");
        AnsiConsole.MarkupLine("");
    }
}

// ─────────────────────────────────────────────────────────────
//  DI Registrar for Spectre.Console.Cli
// ─────────────────────────────────────────────────────────────
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    private ServiceProvider? _provider;

    public ITypeResolver Build()
    {
        _provider = services.BuildServiceProvider();
        return new TypeResolver(_provider);
    }

    public void Register(Type service, Type implementation)
        => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation)
        => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
        => services.AddSingleton(service, _ => factory());
}

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver
{
    public object? Resolve(Type? type)
    {
        if (type == null) return null;
        return provider.GetService(type) ?? Activator.CreateInstance(type);
    }

    public void Dispose() { }
}
