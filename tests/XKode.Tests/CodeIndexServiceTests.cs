using XKode.Services;
using Xunit;

namespace XKode.Tests;

public class CodeIndexServiceTests
{
    [Theory]
    [InlineData("```javascript", "Code — JavaScript")]
    [InlineData("```js",         "Code — JavaScript")]
    [InlineData("```typescript", "Code — TypeScript")]
    [InlineData("```ts",         "Code — TypeScript")]
    [InlineData("```csharp",     "Code — C#")]
    [InlineData("```cs",         "Code — C#")]
    [InlineData("```c#",         "Code — C#")]
    [InlineData("```python",     "Code — Python")]
    public void FormatCodeHeader_Normalizes_KnownLanguages(string raw, string expected)
    {
        var result = CodeIndexService.FormatCodeHeader(raw);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("```", "Code")]
    [InlineData("``` ", "Code")]
    [InlineData("```unknown", "Code")]
    public void FormatCodeHeader_UnknownOrMissingLanguage_FallsBackToCode(string raw, string expected)
    {
        var result = CodeIndexService.FormatCodeHeader(raw);
        Assert.Equal(expected, result);
    }
}

