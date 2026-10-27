namespace Core.Llm;

/// <param name="Prompt">The full prompt sent to the model.</param>
/// <param name="JsonSchema">The JSON schema the output must follow.</param>
/// <param name="CaseId">Optional id used by tests and evaluations to select a fixture.</param>
public sealed record LlmRequest(string Prompt, string JsonSchema, string? CaseId = null);
