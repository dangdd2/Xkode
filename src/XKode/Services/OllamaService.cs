using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XKode.Services;

// ─────────────────────────────────────────────────────────────
//  OllamaService — Communicates with local Ollama HTTP API
//
//  Fix log:
//  - OllamaUrl reads OLLAMA_HOST env var (default localhost:11434)
//  - EnsureBaseAddress() guards against unconfigured HttpClient
//  - IsAvailableAsync uses 3-second timeout — fails fast, no hang
//  - ChatStreamAsync distinguishes timeout vs connection refused
//  - PrintConnectionHelp() shows actionable fix steps in terminal
// ─────────────────────────────────────────────────────────────
public class OllamaService(HttpClient http)
{
    private string _model = "qwen2.5-coder:7b";

    // Reads OLLAMA_HOST env var, falls back to localhost:11434
    public static string OllamaUrl =>
        Environment.GetEnvironmentVariable("OLLAMA_HOST")
        ?? "http://localhost:11434";

    public string CurrentModel => _model;
    public void SetModel(string model) => _model = model;

    // Fallback: set BaseAddress if DI didn't configure it
    private void EnsureBaseAddress()
    {
        if (http.BaseAddress == null)
            http.BaseAddress = new Uri(OllamaUrl);
    }

    // ── Streaming chat ──────────────────────────────────────
    public async IAsyncEnumerable<string> ChatStreamAsync(
        List<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        EnsureBaseAddress();

        var request = new ChatRequest { Model = _model, Messages = history, Stream = true };
        var json = JsonSerializer.Serialize(request, JsonCtx.Default.ChatRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("/api/chat", content, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (TaskCanceledException)
        {
            throw new OllamaException(
                "Request timed out. Ollama may be overloaded or the model is too large.\n" +
                "Try a smaller model: ollamacode chat --model llama3.2:3b");
        }
        catch (HttpRequestException ex)
        {
            throw new OllamaException(BuildConnectionError(ex));
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            ChatStreamChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize(line, JsonCtx.Default.ChatStreamChunk); }
            catch { continue; }

            if (chunk?.Message?.Content is { Length: > 0 } text)
                yield return text;

            if (chunk?.Done == true) break;
        }
    }

    // ── Single completion (non-streaming) ──────────────────
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage> { new() { Role = "user", Content = prompt } };
        var sb = new StringBuilder();
        await foreach (var chunk in ChatStreamAsync(messages, ct))
            sb.Append(chunk);
        return sb.ToString();
    }

    // ── List available models ───────────────────────────────
    public async Task<List<OllamaModel>> ListModelsAsync(CancellationToken ct = default)
    {
        EnsureBaseAddress();
        try
        {
            var resp = await http.GetFromJsonAsync<ModelsResponse>(
                "/api/tags", JsonCtx.Default.ModelsResponse, ct);
            return resp?.Models ?? [];
        }
        catch { return []; }
    }

    // ── Health check — short 3 s timeout, no hang ──────────
    public async Task<bool> IsAvailableAsync()
    {
        EnsureBaseAddress();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            var resp = await http.GetAsync("/api/tags", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Print actionable fix steps to stderr ────────────────
    public static void PrintConnectionHelp()
    {
        var url = OllamaUrl;
        Console.Error.WriteLine();
        Console.Error.WriteLine("┌─ How to fix ──────────────────────────────────────┐");
        Console.Error.WriteLine($"│  Tried: {url,-43}│");
        Console.Error.WriteLine("│                                                    │");
        Console.Error.WriteLine("│  1. Install Ollama → https://ollama.ai             │");
        Console.Error.WriteLine("│  2. Start it       → ollama serve                 │");
        Console.Error.WriteLine("│  3. Pull a model   → ollama pull qwen2.5-coder:7b │");
        Console.Error.WriteLine("│  4. Custom host?   → OLLAMA_HOST=http://IP:11434  │");
        Console.Error.WriteLine("└────────────────────────────────────────────────────┘");
        Console.Error.WriteLine();
    }

    // ─── Private ────────────────────────────────────────────
    private static string BuildConnectionError(HttpRequestException ex)
    {
        var inner = ex.InnerException?.Message ?? ex.Message;
        if (inner.Contains("refused") || inner.Contains("actively refused"))
            return $"Ollama is not running at {OllamaUrl}.\nStart it with: ollama serve";
        if (inner.Contains("No such host") || inner.Contains("Name or service not known"))
            return $"Cannot resolve host: {OllamaUrl}.\nCheck your OLLAMA_HOST env var.";
        return $"Cannot connect to Ollama at {OllamaUrl}.\n{inner}";
    }
}

// ─────────────────────────────────────────────────────────────
//  Models (JSON serialization)
// ─────────────────────────────────────────────────────────────
public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;
}

public class ChatStreamChunk
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("modified_at")]
    public DateTime ModifiedAt { get; set; }
}

public class ModelsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel> Models { get; set; } = [];
}

public class OllamaException(string message) : Exception(message) { }

// Source-generated JSON context for AOT/performance
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ChatStreamChunk))]
[JsonSerializable(typeof(ModelsResponse))]
[JsonSerializable(typeof(OllamaModel))]
[JsonSerializable(typeof(List<ChatMessage>))]
internal partial class JsonCtx : JsonSerializerContext { }
