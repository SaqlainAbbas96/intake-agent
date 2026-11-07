using System.Net;
using System.Text;
using System.Text.Json;
using Core.Llm;
using Infrastructure.Llm;

namespace Api.Tests.Llm;

public class GeminiLlmProviderTests
{
    private const string Schema = """{"type":"object","properties":{"id":{"type":"string"}}}""";

    private static HttpResponseMessage Ok(string text, string finishReason = "STOP") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    candidates = new[]
                    {
                        new { content = new { parts = new[] { new { text } } }, finishReason }
                    },
                    usageMetadata = new { promptTokenCount = 10, candidatesTokenCount = 5, thoughtsTokenCount = 3 }
                }),
                Encoding.UTF8, "application/json")
        };

    private static GeminiLlmProvider Create(
        FakeHttpMessageHandler handler, int maxRetries = 2, TimeSpan? timeout = null, List<TimeSpan>? delays = null)
    {
        var options = new GeminiOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            MaxRetries = maxRetries,
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = Timeout.InfiniteTimeSpan
        };
        return new GeminiLlmProvider(http, options)
        {
            DelayAsync = (delay, _) =>
            {
                delays?.Add(delay);
                return Task.CompletedTask;
            }
        };
    }

    [Fact]
    public async Task Returns_content_and_usage_on_success()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Ok("""{"id":"A1"}""")));

        var response = await Create(handler).CompleteAsync(new LlmRequest("prompt", Schema));

        Assert.Equal("""{"id":"A1"}""", response.Content);
        Assert.Equal(new LlmUsage(10, 8), response.Usage); // 5 output + 3 thinking tokens
        Assert.Equal("gemini", response.Provider);
        Assert.Equal("test-model", response.Model);
    }

    [Fact]
    public async Task Sends_api_key_and_schema_for_structured_output()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Ok("{}")));

        await Create(handler).CompleteAsync(new LlmRequest("prompt", Schema));

        Assert.Equal("test-key", handler.ApiKeys[0]);
        Assert.Contains("responseSchema", handler.Bodies[0]);
        Assert.Contains("application/json", handler.Bodies[0]);
    }

    [Fact]
    public async Task Retries_after_429_then_succeeds()
    {
        var delays = new List<TimeSpan>();
        var handler = new FakeHttpMessageHandler((call, _) => Task.FromResult(
            call == 1 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : Ok("{}")));

        var response = await Create(handler, delays: delays).CompleteAsync(new LlmRequest("prompt", Schema));

        Assert.Equal(2, handler.CallCount);
        Assert.Single(delays);
        Assert.Equal("{}", response.Content);
    }

    [Fact]
    public async Task Gives_up_after_max_retries_with_a_clear_error()
    {
        var delays = new List<TimeSpan>();
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(
            () => Create(handler, maxRetries: 2, delays: delays).CompleteAsync(new LlmRequest("prompt", Schema)));

        Assert.Equal(3, handler.CallCount);
        Assert.Equal(2, delays.Count);
        Assert.Contains("3 attempts", exception.Message);
    }

    [Fact]
    public async Task Does_not_retry_on_client_errors()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)));

        await Assert.ThrowsAsync<LlmProviderException>(
            () => Create(handler).CompleteAsync(new LlmRequest("prompt", Schema)));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Times_out_and_retries()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage();
        });

        await Assert.ThrowsAsync<LlmProviderException>(
            () => Create(handler, maxRetries: 1, timeout: TimeSpan.FromMilliseconds(50))
                .CompleteAsync(new LlmRequest("prompt", Schema)));

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Rejects_output_that_was_cut_off()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(Ok("""{"id":""", "MAX_TOKENS")));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(
            () => Create(handler).CompleteAsync(new LlmRequest("prompt", Schema)));

        Assert.Contains("MAX_TOKENS", exception.Message);
    }
}
