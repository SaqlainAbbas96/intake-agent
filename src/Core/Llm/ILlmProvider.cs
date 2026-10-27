namespace Core.Llm;

public interface ILlmProvider
{
    string Name { get; }

    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
