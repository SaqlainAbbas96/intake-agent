using Core.Llm;
using Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Llm;

public class LlmLoggingAndCostTests
{
    [Fact]
    public void Estimates_cost_from_usage_and_pricing()
    {
        var cost = LlmCostEstimator.Estimate(new LlmUsage(1000, 500), new LlmPricing(0.10m, 0.40m));

        Assert.Equal(0.0003m, cost);
    }

    [Fact]
    public async Task Logs_usage_latency_and_estimated_cost()
    {
        var logger = new ListLogger<LoggingLlmProvider>();
        var provider = new LoggingLlmProvider(new MockLlmProvider(), new LlmPricing(1m, 2m), logger);

        await provider.CompleteAsync(new LlmRequest("Extract the order from this email.", "{}"));

        var message = Assert.Single(logger.Messages);
        Assert.Contains("Provider=mock", message);
        Assert.Contains("PromptTokens=", message);
        Assert.Contains("LatencyMs=", message);
        Assert.Contains("EstimatedCostUsd=", message);
    }

    [Fact]
    public void Gemini_without_an_api_key_fails_fast_with_a_clear_message()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LLM_PROVIDER"] = "gemini" })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddLlmProvider(configuration));

        Assert.Contains("GEMINI_API_KEY", exception.Message);
    }
}
