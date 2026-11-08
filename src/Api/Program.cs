using Api.Endpoints;

using Infrastructure.Data;
using Infrastructure.Extraction;
using Infrastructure.Llm;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddDbContext<IntakeDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddLlmProvider(builder.Configuration);
builder.Services.AddExtraction();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("RUN_MIGRATIONS"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<IntakeDbContext>().Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapExtractEndpoints();

app.Run();

// Needed so the test project can reference Program
public partial class Program { }
