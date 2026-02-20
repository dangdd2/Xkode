using XKode.Services;

namespace XKode.Agents;

// ─────────────────────────────────────────────────────────────
//  IAgent — Base interface for all multi-agent system agents
//
//  Agents are specialized AI assistants that handle specific
//  parts of the development workflow:
//    - PlannerAgent: Breaks down tasks into steps
//    - ExecutorAgent: Implements code changes
//    - ReviewerAgent: Checks code quality
// ─────────────────────────────────────────────────────────────
public interface IAgent
{
    /// <summary>
    /// Agent name for logging and UI display
    /// </summary>
    string Name { get; }

    /// <summary>
    /// System prompt that defines agent behavior
    /// </summary>
    string SystemPrompt { get; }

    /// <summary>
    /// Ollama model this agent uses (can differ per agent)
    /// </summary>
    string Model { get; }

    /// <summary>
    /// Execute agent's primary task
    /// </summary>
    /// <param name="input">Input context/prompt for the agent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Agent's output (format depends on agent type)</returns>
    Task<string> ExecuteAsync(string input, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────
//  AgentBase — Base implementation with common functionality
// ─────────────────────────────────────────────────────────────
public abstract class AgentBase(OllamaService ollama, string name, string model) : IAgent
{
    protected readonly OllamaService _ollama = ollama;

    public string Name { get; } = name;
    public string Model { get; } = model;
    public abstract string SystemPrompt { get; }

    public virtual async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        // Build conversation with system prompt
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = SystemPrompt },
            new() { Role = "user", Content = input }
        };

        var originalModel = _ollama.CurrentModel;

        try
        {
            // Switch to agent-specific model
            _ollama.SetModel(Model);

            // Use non-streaming complete for agents to avoid truncation
            // Agents need full JSON responses, streaming can cause issues
            var result = new System.Text.StringBuilder();
            await foreach (var chunk in _ollama.ChatStreamAsync(messages, ct))
            {
                result.Append(chunk);
            }

            return result.ToString();
        }
        finally
        {
            // Restore original model
            _ollama.SetModel(originalModel);
        }
    }
}
