namespace Core.Llm;

/// <summary>Price per one million tokens, in US dollars.</summary>
public sealed record LlmPricing(decimal InputUsdPerMillionTokens = 0m, decimal OutputUsdPerMillionTokens = 0m);
