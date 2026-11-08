using Core.Llm;

namespace Core.Extraction;

public sealed record ValidationIssue(string Path, string Message);

public abstract record ExtractionOutcome
{
    public sealed record Success(
        Guid RequestId,
        string DataJson,
        string Provider,
        string Model,
        LlmUsage Usage,
        int LatencyMs,
        decimal EstimatedCostUsd) : ExtractionOutcome;

    public sealed record InvalidInput(string Message) : ExtractionOutcome;

    public sealed record UnknownSchema(string Name, IReadOnlyCollection<string> Available) : ExtractionOutcome;

    public sealed record ValidationFailed(Guid RequestId, IReadOnlyList<ValidationIssue> Issues) : ExtractionOutcome;

    public sealed record ProviderFailed(Guid RequestId, string Message) : ExtractionOutcome;
}
