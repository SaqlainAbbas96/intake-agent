namespace Core.Llm;

public sealed record LlmResponse(
    string Content,
    LlmUsage Usage,
    TimeSpan Latency,
    string Provider,
    string Model);
