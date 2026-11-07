using System.Globalization;

using Core.Llm;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Llm;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlmProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = (configuration["LLM_PROVIDER"] ?? "mock").Trim().ToLowerInvariant();
        var pricing = new LlmPricing(
            ReadDecimal(configuration, "LLM_INPUT_USD_PER_MILLION"),
            ReadDecimal(configuration, "LLM_OUTPUT_USD_PER_MILLION"));

        switch (provider)
        {
            case "mock":
                var fixturesPath = configuration["MOCK_FIXTURES_PATH"];
                services.AddSingleton<ILlmProvider>(sp => new LoggingLlmProvider(
                    string.IsNullOrWhiteSpace(fixturesPath)
                        ? new MockLlmProvider()
                        : MockLlmProvider.FromDirectory(fixturesPath),
                    pricing,
                    sp.GetRequiredService<ILogger<LoggingLlmProvider>>()));
                break;

            case "gemini":
                var options = GeminiOptions.FromConfiguration(configuration);
                services.AddSingleton(options);
                services.AddHttpClient<GeminiLlmProvider>(client =>
                {
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = Timeout.InfiniteTimeSpan; // timeouts are handled per attempt in the provider
                });
                services.AddTransient<ILlmProvider>(sp => new LoggingLlmProvider(
                    sp.GetRequiredService<GeminiLlmProvider>(),
                    pricing,
                    sp.GetRequiredService<ILogger<LoggingLlmProvider>>()));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown LLM_PROVIDER '{provider}'. Supported values: mock, gemini.");
        }

        return services;
    }

    private static decimal ReadDecimal(IConfiguration configuration, string key) =>
        decimal.TryParse(configuration[key], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
}
