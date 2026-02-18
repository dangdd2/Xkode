using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XKode.Services;
using Xunit;

namespace XKode.Tests;

public class OllamaServiceTests
{
    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request, cancellationToken));
    }

    private static OllamaService CreateService(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new TestHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var config = new ConfigService
        {
            OllamaUrl = "http://localhost:11434"
        };

        return new OllamaService(http, config);
    }

    [Fact]
    public async Task ChatStreamAsync_Yields_Text_From_Stream()
    {
        // Arrange
        var lines = new[]
        {
            JsonSerializer.Serialize(new ChatStreamChunk
            {
                Message = new ChatMessage { Role = "assistant", Content = "Hello" },
                Done = false
            }),
            JsonSerializer.Serialize(new ChatStreamChunk
            {
                Message = new ChatMessage { Role = "assistant", Content = " world" },
                Done = true
            })
        };

        var body = string.Join("\n", lines) + "\n";

        var service = CreateService((req, _) =>
        {
            Assert.Equal("/api/chat", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson")
            };
        });

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hi" }
        };

        // Act
        var received = new StringBuilder();
        await foreach (var chunk in service.ChatStreamAsync(history))
            received.Append(chunk);

        // Assert
        Assert.Equal("Hello world", received.ToString());
    }

    [Fact]
    public async Task CompleteAsync_Concatenates_Streamed_Chunks()
    {
        // Arrange
        var lines = new[]
        {
            JsonSerializer.Serialize(new ChatStreamChunk
            {
                Message = new ChatMessage { Role = "assistant", Content = "Line1 " },
                Done = false
            }),
            JsonSerializer.Serialize(new ChatStreamChunk
            {
                Message = new ChatMessage { Role = "assistant", Content = "Line2" },
                Done = true
            })
        };

        var body = string.Join("\n", lines) + "\n";

        var service = CreateService((req, _) =>
        {
            Assert.Equal("/api/chat", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson")
            };
        });

        // Act
        var result = await service.CompleteAsync("test");

        // Assert
        Assert.Equal("Line1 Line2", result);
    }

    [Fact]
    public async Task ListModelsAsync_Parses_Models()
    {
        // Arrange
        var models = new ModelsResponse
        {
            Models =
            [
                new OllamaModel
                {
                    Name = "qwen2.5-coder:7b",
                    Size = 1024,
                    ModifiedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var json = JsonSerializer.Serialize(models);

        var service = CreateService((req, _) =>
        {
            Assert.Equal("/api/tags", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        // Act
        var result = await service.ListModelsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("qwen2.5-coder:7b", result[0].Name);
        Assert.Equal(1024, result[0].Size);
    }

    [Fact]
    public async Task IsAvailableAsync_Returns_True_On_Success()
    {
        // Arrange
        var service = CreateService((req, _) =>
        {
            Assert.Equal("/api/tags", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Act
        var available = await service.IsAvailableAsync();

        // Assert
        Assert.True(available);
    }

    [Fact]
    public async Task IsAvailableAsync_Returns_False_On_Error()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            throw new HttpRequestException("boom");
        });

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var config = new ConfigService
        {
            OllamaUrl = "http://localhost:11434"
        };

        var service = new OllamaService(http, config);

        // Act
        var available = await service.IsAvailableAsync();

        // Assert
        Assert.False(available);
    }

    [Fact]
    public async Task ChatStreamAsync_Timeout_Throws_OllamaException()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            throw new TaskCanceledException("timeout");
        });

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var config = new ConfigService
        {
            OllamaUrl = "http://localhost:11434"
        };

        var service = new OllamaService(http, config);

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hi" }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<OllamaException>(async () =>
        {
            await foreach (var _ in service.ChatStreamAsync(history))
            {
                // consume
            }
        });

        Assert.Contains("Request timed out", ex.Message);
    }
}

