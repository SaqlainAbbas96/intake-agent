using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extraction;

public static class ExtractionServiceCollectionExtensions
{
    public static IServiceCollection AddExtraction(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SchemaValidator>();
        services.AddScoped<ExtractionService>();
        return services;
    }
}
