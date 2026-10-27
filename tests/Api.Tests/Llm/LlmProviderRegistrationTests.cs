using System.Text.Json;

using Core.Llm;

using Infrastructure.Llm;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Llm;

public class LlmProviderRegistrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LlmProviderRegistrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Mock_provider_is_the_default_and_needs_no_api_key()
    {
        var provider = _factory.Services.GetRequiredService<ILlmProvider>();

        var response = await provider.CompleteAsync(new LlmRequest("Extract the order.", "{}"));

        Assert.Equal("mock", provider.Name);
        using var document = JsonDocument.Parse(response.Content); // valid JSON
        Assert.True(response.Usage.TotalTokens > 0);
    }

    [Fact]
    public void Unknown_provider_fails_fast_with_a_clear_message()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LLM_PROVIDER"] = "bogus" })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddLlmProvider(configuration));

        Assert.Contains("bogus", exception.Message);
    }
}
