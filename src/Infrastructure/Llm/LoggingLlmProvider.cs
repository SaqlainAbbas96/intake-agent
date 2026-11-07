using Core.Llm;

using Microsoft.Extensions.Logging;

namespace Infrastructure.Llm;

/// <summary>Wraps any provider and logs tokens, latency and estimated cost for every call.</summary>
public sealed class LoggingLlmProvider(
    ILlmProvider inner,
    LlmPricing pricing,
    ILogger<LoggingLlmProvider> logger) : ILlmProvider
{
    public string Name => inner.Name;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await inner.CompleteAsync(request, cancellationToken);
            var cost = LlmCostEstimator.Estimate(response.Usage, pricing);

            logger.LogInformation(
                "LLM call completed. Provider={Provider} Model={Model} PromptTokens={PromptTokens} " +
                "CompletionTokens={CompletionTokens} LatencyMs={LatencyMs} EstimatedCostUsd={EstimatedCostUsd:F6}",
                response.Provider, response.Model, response.Usage.PromptTokens,
                response.Usage.CompletionTokens, (int)response.Latency.TotalMilliseconds, cost);

            return response;
        }
        catch (LlmProviderException ex)
        {
            logger.LogWarning(ex, "LLM call failed. Provider={Provider}", inner.Name);
            throw;
        }
    }
}
