using XKode.Models;

namespace XKode.Models;

/// <summary>
/// Represents an interactive agent session state
/// </summary>
public class AgentSession
{
    /// <summary>
    /// Current execution plan
    /// </summary>
    public ExecutionPlan? CurrentPlan { get; set; }

    /// <summary>
    /// Current agent name
    /// </summary>
    public string CurrentAgent { get; set; } = "planner";

    /// <summary>
    /// Conversation history
    /// </summary>
    public List<string> History { get; set; } = new();

    /// <summary>
    /// Session start time
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Completed steps count
    /// </summary>
    public int CompletedSteps { get; set; }

    /// <summary>
    /// Project root path
    /// </summary>
    public string ProjectRoot { get; set; } = "";

    /// <summary>
    /// Session is running
    /// </summary>
    public bool IsRunning { get; set; } = true;

    /// <summary>
    /// Get session duration
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - StartedAt;

    /// <summary>
    /// Add to history
    /// </summary>
    public void AddToHistory(string entry)
    {
        History.Add($"[{DateTime.Now:HH:mm:ss}] {entry}");
        
        // Keep only last 50 entries
        if (History.Count > 50)
        {
            History.RemoveAt(0);
        }
    }
}
