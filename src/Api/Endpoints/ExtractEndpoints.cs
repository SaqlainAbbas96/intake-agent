using System.Text.Json;

using Core.Extraction;

using Infrastructure.Extraction;

namespace Api.Endpoints;

public sealed record ExtractRequest(string? Text, string? Schema, string? CaseId);

public sealed record UsageInfo(int PromptTokens, int CompletionTokens, int LatencyMs, decimal EstimatedCostUsd);

public sealed record ExtractResponse(
    Guid RequestId, string Status, JsonElement Data, UsageInfo Usage, string Provider, string Model);

public static class ExtractEndpoints
{
    public static void MapExtractEndpoints(this WebApplication app)
    {
        app.MapPost("/extract", async (ExtractRequest request, ExtractionService service, CancellationToken ct) =>
        {
            var outcome = await service.ExtractAsync(request.Schema, request.Text, request.CaseId, ct);

            return outcome switch
            {
                ExtractionOutcome.Success s => Results.Ok(new ExtractResponse(
                    s.RequestId,
                    "Completed",
                    JsonDocument.Parse(s.DataJson).RootElement.Clone(),
                    new UsageInfo(s.Usage.PromptTokens, s.Usage.CompletionTokens, s.LatencyMs, s.EstimatedCostUsd),
                    s.Provider,
                    s.Model)),

                ExtractionOutcome.InvalidInput i => Results.Problem(
                    title: "Invalid input", detail: i.Message, statusCode: 400,
                    type: "urn:intake-agent:error:invalid-input"),

                ExtractionOutcome.UnknownSchema u => Results.Problem(
                    title: "Unknown schema", detail: $"Schema '{u.Name}' does not exist.", statusCode: 400,
                    type: "urn:intake-agent:error:unknown-schema",
                    extensions: new Dictionary<string, object?> { ["availableSchemas"] = u.Available }),

                ExtractionOutcome.ValidationFailed v => Results.Problem(
                    title: "Model output failed schema validation", statusCode: 422,
                    type: "urn:intake-agent:error:validation-failed",
                    extensions: new Dictionary<string, object?>
                    {
                        ["requestId"] = v.RequestId,
                        ["errors"] = v.Issues
                    }),

                ExtractionOutcome.ProviderFailed p => Results.Problem(
                    title: "LLM provider failed", detail: p.Message, statusCode: 502,
                    type: "urn:intake-agent:error:provider-failed",
                    extensions: new Dictionary<string, object?> { ["requestId"] = p.RequestId }),

                _ => throw new InvalidOperationException("Unhandled extraction outcome.")
            };
        });
    }
}
