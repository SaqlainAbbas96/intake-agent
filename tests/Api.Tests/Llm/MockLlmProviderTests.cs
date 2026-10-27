using Core.Llm;

using Infrastructure.Llm;

namespace Api.Tests.Llm;

public class MockLlmProviderTests
{
    [Fact]
    public async Task Returns_fixture_for_known_case_id()
    {
        var provider = new MockLlmProvider(new Dictionary<string, string>
        {
            ["case-001"] = """{"order_id":"A1"}"""
        });

        var response = await provider.CompleteAsync(new LlmRequest("prompt", "{}", "case-001"));

        Assert.Equal("""{"order_id":"A1"}""", response.Content);
        Assert.Equal("mock", response.Provider);
    }

    [Fact]
    public async Task Returns_default_content_when_no_case_id()
    {
        var provider = new MockLlmProvider();

        var response = await provider.CompleteAsync(new LlmRequest("prompt", "{}"));

        Assert.Equal(MockLlmProvider.DefaultContent, response.Content);
    }

    [Fact]
    public async Task Throws_for_unknown_case_id()
    {
        var provider = new MockLlmProvider();

        await Assert.ThrowsAsync<LlmProviderException>(
            () => provider.CompleteAsync(new LlmRequest("prompt", "{}", "missing")));
    }

    [Fact]
    public async Task Same_input_gives_same_output_and_usage()
    {
        var provider = new MockLlmProvider();
        var request = new LlmRequest("Extract the order from this email.", "{}");

        var first = await provider.CompleteAsync(request);
        var second = await provider.CompleteAsync(request);

        Assert.Equal(first.Content, second.Content);
        Assert.Equal(first.Usage, second.Usage);
    }

    [Fact]
    public async Task Reports_usage_and_latency()
    {
        var provider = new MockLlmProvider();

        var response = await provider.CompleteAsync(new LlmRequest("Extract the order from this email.", "{}"));

        Assert.True(response.Usage.PromptTokens > 0);
        Assert.True(response.Usage.CompletionTokens > 0);
        Assert.True(response.Latency > TimeSpan.Zero);
    }
}
