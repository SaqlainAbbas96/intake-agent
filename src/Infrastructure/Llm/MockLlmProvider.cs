using Core.Llm;

namespace Infrastructure.Llm;

public sealed class MockLlmProvider : ILlmProvider
{
    public const string DefaultContent = """{"summary":"mock extraction"}""";

    private readonly IReadOnlyDictionary<string, string> _fixtures;
    private readonly string _defaultContent;

    public MockLlmProvider(
        IReadOnlyDictionary<string, string>? fixtures = null,
        string? defaultContent = null)
    {
        _fixtures = fixtures ?? new Dictionary<string, string>();
        _defaultContent = defaultContent ?? DefaultContent;
    }

    public string Name => "mock";

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string content;
        if (request.CaseId is null)
            content = _defaultContent;
        else if (_fixtures.TryGetValue(request.CaseId, out var found))
            content = found;
        else
            throw new LlmProviderException($"No mock fixture for case '{request.CaseId}'.");

        // Deterministic fake usage: roughly 4 characters per token
        var usage = new LlmUsage(
            PromptTokens: Math.Max(1, request.Prompt.Length / 4),
            CompletionTokens: Math.Max(1, content.Length / 4));

        return Task.FromResult(new LlmResponse(content, usage, TimeSpan.FromMilliseconds(1), Name, "mock-model"));
    }

    /// <summary>Loads fixtures from a folder of JSON files. The file name (without .json) is the case id.</summary>
    public static MockLlmProvider FromDirectory(string directory)
    {
        var fixtures = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.json")
                .ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => File.ReadAllText(f), StringComparer.Ordinal)
            : new Dictionary<string, string>();

        return new MockLlmProvider(fixtures);
    }
}
