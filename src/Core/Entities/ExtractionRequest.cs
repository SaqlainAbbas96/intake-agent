namespace Core.Entities;

public class ExtractionRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string SchemaName { get; set; } = "";
    public string RawInput { get; set; } = "";
    public ExtractionStatus Status { get; set; } = ExtractionStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
}