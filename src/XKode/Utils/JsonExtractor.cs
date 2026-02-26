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

        // Find JSON object boundaries using proper brace matching
        var startIndex = text.IndexOf('{');
        if (startIndex < 0)
        {
            return "{}"; // No opening brace found
        }

        var endIndex = FindMatchingBrace(text, startIndex);
        if (endIndex < 0)
        {
            return "{}"; // No matching closing brace
        }

        text = text[startIndex..(endIndex + 1)];

        return text.Trim();
    }

    /// <summary>
    /// Find the matching closing brace for an opening brace
    /// </summary>
    private static int FindMatchingBrace(string text, int startIndex)
    {
        if (startIndex < 0 || startIndex >= text.Length || text[startIndex] != '{')
            return -1;

        int braceCount = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '{')
                    braceCount++;
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                        return i; // Found matching brace
                }
            }
        }

        return -1; // No matching brace found
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
