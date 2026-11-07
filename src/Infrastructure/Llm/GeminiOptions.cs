using Microsoft.Extensions.Configuration;

namespace Infrastructure.Llm;

public sealed class GeminiOptions
{
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "";
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; init; } = 3;
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromSeconds(10);
    public int MaxOutputTokens { get; init; } = 4096;

    public static GeminiOptions FromConfiguration(IConfiguration configuration)
    {
        var apiKey = configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("GEMINI_API_KEY is required when LLM_PROVIDER=gemini.");

        var model = configuration["GEMINI_MODEL"];
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                "GEMINI_MODEL is required when LLM_PROVIDER=gemini. Use a model id from Google AI Studio.");

        return new GeminiOptions { ApiKey = apiKey, Model = model };
    }
}
