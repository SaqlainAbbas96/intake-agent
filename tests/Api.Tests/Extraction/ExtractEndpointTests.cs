using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Core.Entities;

using Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Extraction;

public class ExtractEndpointTests(ExtractApiFactory factory) : IClassFixture<ExtractApiFactory>
{
    private const string Text = "Hi, this is Dana Reyes. Please send 12 units.";

    private async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(object body)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/extract", body);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response.StatusCode, json);
    }

    private async Task<(ExtractionRequest Request, ExtractionResult? Result)> LoadAsync(Guid requestId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
        var request = await db.ExtractionRequests.SingleAsync(r => r.Id == requestId);
        var result = await db.ExtractionResults.SingleOrDefaultAsync(r => r.RequestId == requestId);
        return (request, result);
    }

    [Fact]
    public async Task Valid_output_returns_data_and_is_persisted()
    {
        var (status, body) = await PostAsync(new { text = Text, schema = "order", caseId = "valid-order" });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Dana Reyes", body.GetProperty("data").GetProperty("customer_name").GetString());
        Assert.Equal(12, body.GetProperty("data").GetProperty("quantity").GetInt32());
        Assert.True(body.GetProperty("usage").GetProperty("promptTokens").GetInt32() > 0);

        var (request, result) = await LoadAsync(body.GetProperty("requestId").GetGuid());
        Assert.Equal(ExtractionStatus.Completed, request.Status);
        Assert.NotNull(result);
        Assert.True(result!.IsSchemaValid);
        Assert.Equal("mock", result.Provider);
    }

    [Fact]
    public async Task Output_missing_a_required_field_returns_422_with_errors()
    {
        var (status, body) = await PostAsync(new { text = Text, schema = "order", caseId = "invalid-order" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());

        var (request, result) = await LoadAsync(body.GetProperty("requestId").GetGuid());
        Assert.Equal(ExtractionStatus.Failed, request.Status);
        Assert.False(result!.IsSchemaValid);
    }

    [Fact]
    public async Task Output_that_is_not_json_returns_422_and_keeps_the_raw_output()
    {
        var (status, body) = await PostAsync(new { text = Text, schema = "order", caseId = "not-json" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);

        var (_, result) = await LoadAsync(body.GetProperty("requestId").GetGuid());
        Assert.Contains("raw_output", result!.ExtractedJson);
    }

    [Fact]
    public async Task Unknown_schema_returns_400_with_available_schemas()
    {
        var (status, body) = await PostAsync(new { text = Text, schema = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("order", body.GetProperty("availableSchemas").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Empty_text_returns_400()
    {
        var (status, _) = await PostAsync(new { text = "  ", schema = "order" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Provider_failure_returns_502_and_marks_the_request_failed()
    {
        var (status, body) = await PostAsync(new { text = Text, schema = "order", caseId = "no-such-fixture" });

        Assert.Equal(HttpStatusCode.BadGateway, status);

        var (request, result) = await LoadAsync(body.GetProperty("requestId").GetGuid());
        Assert.Equal(ExtractionStatus.Failed, request.Status);
        Assert.Null(result);
    }
}
