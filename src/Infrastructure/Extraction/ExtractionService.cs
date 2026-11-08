using System.Text.Json;

using Core.Entities;
using Core.Extraction;
using Core.Llm;

using Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Extraction;

public sealed class ExtractionService(
    IntakeDbContext db,
    ILlmProvider llm,
    SchemaValidator validator,
    LlmPricing pricing,
    TimeProvider clock)
{
    public const int MaxTextLength = 20_000;
    public static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000001");

    public async Task<ExtractionOutcome> ExtractAsync(
        string? schemaName, string? text, string? caseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ExtractionOutcome.InvalidInput("'text' is required.");
        if (text.Length > MaxTextLength)
            return new ExtractionOutcome.InvalidInput($"'text' must be at most {MaxTextLength} characters.");

        var name = schemaName?.Trim() ?? "";
        if (!ExtractionSchemas.TryGet(name, out var schemaJson))
            return new ExtractionOutcome.UnknownSchema(name, ExtractionSchemas.Names);

        await EnsureDefaultTenantAsync(cancellationToken);

        var request = new ExtractionRequest
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            SchemaName = name.ToLowerInvariant(),
            RawInput = text,
            Status = ExtractionStatus.Pending,
            CreatedAt = clock.GetUtcNow()
        };
        db.ExtractionRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        LlmResponse response;
        try
        {
            response = await llm.CompleteAsync(
                new LlmRequest(ExtractionPrompt.Build(text), schemaJson, caseId), cancellationToken);
        }
        catch (LlmProviderException)
        {
            request.Status = ExtractionStatus.Failed;
            await db.SaveChangesAsync(cancellationToken);
            return new ExtractionOutcome.ProviderFailed(request.Id, "The model provider failed.");
        }

        var content = response.Content;
        var isJson = IsValidJson(content);
        IReadOnlyList<ValidationIssue> issues = isJson
            ? await validator.ValidateAsync(schemaJson, content)
            : new[] { new ValidationIssue("#", "Output is not valid JSON.") };
        var isValid = issues.Count == 0;

        var cost = LlmCostEstimator.Estimate(response.Usage, pricing);

        db.ExtractionResults.Add(new ExtractionResult
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            RequestId = request.Id,
            // the column is jsonb, so non-JSON output is wrapped instead of stored as-is
            ExtractedJson = isJson ? content : JsonSerializer.Serialize(new { raw_output = content }),
            IsSchemaValid = isValid,
            Provider = response.Provider,
            Model = response.Model,
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            LatencyMs = (int)response.Latency.TotalMilliseconds,
            CostUsd = cost,
            CreatedAt = clock.GetUtcNow()
        });
        request.Status = isValid ? ExtractionStatus.Completed : ExtractionStatus.Failed;
        await db.SaveChangesAsync(cancellationToken);

        return isValid
            ? new ExtractionOutcome.Success(
                request.Id, content, response.Provider, response.Model, response.Usage,
                (int)response.Latency.TotalMilliseconds, cost)
            : new ExtractionOutcome.ValidationFailed(request.Id, issues);
    }

    private async Task EnsureDefaultTenantAsync(CancellationToken cancellationToken)
    {
        if (await db.Tenants.AnyAsync(t => t.Id == DefaultTenantId, cancellationToken))
            return;

        db.Tenants.Add(new Tenant { Id = DefaultTenantId, Name = "default", CreatedAt = clock.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear(); // another request created it first
        }
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
