namespace Core.Entities;

public class ExtractionResult
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestId { get; set; }
    public string ExtractedJson { get; set; } = "{}";
    public bool IsSchemaValid { get; set; }
    public decimal? Confidence { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int LatencyMs { get; set; }
    public decimal CostUsd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
