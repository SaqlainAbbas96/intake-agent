namespace Core.Llm;

public static class LlmCostEstimator
{
    public static decimal Estimate(LlmUsage usage, LlmPricing pricing) =>
        usage.PromptTokens / 1_000_000m * pricing.InputUsdPerMillionTokens
        + usage.CompletionTokens / 1_000_000m * pricing.OutputUsdPerMillionTokens;
}
