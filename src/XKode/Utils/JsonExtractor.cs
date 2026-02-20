using System.Text.Json;

namespace XKode.Utils;

// ─────────────────────────────────────────────────────────────
//  JsonExtractor — Robust JSON extraction from LLM responses
//
//  Handles common issues:
//  - "Thinking..." preambles
//  - Markdown fences (```json)
//  - Text after JSON
//  - Truncated responses
// ─────────────────────────────────────────────────────────────
public static class JsonExtractor
{
    /// <summary>
    /// Extract and parse JSON from potentially noisy LLM response
    /// </summary>
    public static T ExtractAndParse<T>(string response, string errorContext = "JSON")
    {
        var json = ExtractJson(response);

        try
        {
            var result = JsonSerializer.Deserialize<T>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
                throw new JsonException($"Failed to deserialize {errorContext} - result was null");

            return result;
        }
        catch (JsonException ex)
        {
            // Show what we tried to parse for debugging
            var preview = json.Length > 500 ? json[..500] + "..." : json;
            throw new JsonException(
                $"Failed to parse {errorContext}: {ex.Message}\n" +
                $"Extracted JSON: {preview}");
        }
    }

    /// <summary>
    /// Extract JSON object from response, removing noise
    /// </summary>
    public static string ExtractJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return "{}";

        var text = response.Trim();

        // Remove markdown fences first
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];
        
        if (text.EndsWith("```"))
            text = text[..^3];

        text = text.Trim();

        // Find JSON object boundaries
        var startIndex = text.IndexOf('{');
        var endIndex = text.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            text = text[startIndex..(endIndex + 1)];
        }
        else
        {
            // No valid JSON brackets found
            return "{}";
        }

        return text.Trim();
    }

    /// <summary>
    /// Check if response looks like it contains JSON
    /// </summary>
    public static bool ContainsJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var text = response.Trim();
        return text.Contains('{') && text.Contains('}');
    }

    /// <summary>
    /// Validate JSON can be parsed before attempting full deserialization
    /// </summary>
    public static bool IsValidJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
