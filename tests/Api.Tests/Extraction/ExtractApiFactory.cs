using Core.Llm;

using Infrastructure.Data;
using Infrastructure.Llm;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Testcontainers.PostgreSql;

namespace Api.Tests.Extraction;

public sealed class ExtractApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    private static readonly Dictionary<string, string> Fixtures = new()
    {
        ["valid-order"] = """{"customer_name":"Dana Reyes","quantity":12}""",
        ["invalid-order"] = """{"customer_name":"Dana Reyes"}""",
        ["not-json"] = "Sorry, I cannot help with that."
    };

    static ExtractApiFactory() =>
        Environment.SetEnvironmentVariable("LLM_PROVIDER", "mock"); // beats any user secrets on your machine

    public ExtractApiFactory()
    {
        _postgres.StartAsync().GetAwaiter().GetResult();

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IntakeDbContext>().Database.Migrate();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<IntakeDbContext>>();
            services.AddDbContext<IntakeDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));

            services.RemoveAll<ILlmProvider>();
            services.AddSingleton<ILlmProvider>(new MockLlmProvider(Fixtures));
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
