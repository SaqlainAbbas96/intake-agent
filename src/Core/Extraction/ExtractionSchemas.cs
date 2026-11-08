using System.Diagnostics.CodeAnalysis;

namespace Core.Extraction;

public static class ExtractionSchemas
{
    private static readonly Dictionary<string, string> Schemas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["order"] = """
            {
              "type": "object",
              "properties": {
                "customer_name": { "type": "string" },
                "quantity": { "type": "integer" },
                "notes": { "type": "string" }
              },
              "required": ["customer_name", "quantity"]
            }
            """
    };

    public static IReadOnlyCollection<string> Names => Schemas.Keys;

    public static bool TryGet(string name, [NotNullWhen(true)] out string? schema) =>
        Schemas.TryGetValue(name, out schema);
}
