using Core.Extraction;

using NJsonSchema;

namespace Infrastructure.Extraction;

public sealed class SchemaValidator
{
    public async Task<IReadOnlyList<ValidationIssue>> ValidateAsync(string schemaJson, string outputJson)
    {
        var schema = await JsonSchema.FromJsonAsync(schemaJson);
        var errors = schema.Validate(outputJson);

        return errors
            .Select(error => new ValidationIssue(error.Path ?? "#", error.Kind.ToString()))
            .ToList();
    }
}
