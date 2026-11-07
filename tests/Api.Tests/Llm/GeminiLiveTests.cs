using System.Text.Json;

using Core.Llm;

using Infrastructure.Llm;

using Xunit.Abstractions;

namespace Api.Tests.Llm;

public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_MODEL")))
            Skip = "Set GEMINI_API_KEY and GEMINI_MODEL to run live tests.";
    }
}

public class GeminiLiveTests(ITestOutputHelper output)
{
    private const string Schema = """
        {"type":"object",
         "properties":{"customer_name":{"type":"string"},"quantity":{"type":"integer"}},
         "required":["customer_name","quantity"]}
        """;

    [LiveFact]
    public async Task Live_call_returns_schema_shaped_output_and_logs_usage()
    {
        var options = new GeminiOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")!,
            Model = Environment.GetEnvironmentVariable("GEMINI_MODEL")!
        };
        using var http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var logger = new ListLogger<LoggingLlmProvider>();
        var provider = new LoggingLlmProvider(new GeminiLlmProvider(http, options), new LlmPricing(), logger);

        var response = await provider.CompleteAsync(new LlmRequest(
            "Extract the order from this message: 'Hi, this is Dana Reyes. Please send 12 units.'", Schema));

        using var document = JsonDocument.Parse(response.Content);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("customer_name").ValueKind);
        Assert.Equal(JsonValueKind.Number, document.RootElement.GetProperty("quantity").ValueKind);
        Assert.True(response.Usage.TotalTokens > 0);

        output.WriteLine(response.Content);
        output.WriteLine(Assert.Single(logger.Messages));
    }
}
