using Core.Llm;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Llm;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlmProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = (configuration["LLM_PROVIDER"] ?? "mock").Trim().ToLowerInvariant();

        switch (provider)
        {
            case "mock":
                var fixturesPath = configuration["MOCK_FIXTURES_PATH"];
                services.AddSingleton<ILlmProvider>(_ =>
                    string.IsNullOrWhiteSpace(fixturesPath)
                        ? new MockLlmProvider()
                        : MockLlmProvider.FromDirectory(fixturesPath));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown LLM_PROVIDER '{provider}'. Supported values: mock.");
        }

        return services;
    }
}
