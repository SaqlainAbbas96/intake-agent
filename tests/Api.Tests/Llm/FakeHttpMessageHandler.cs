using System.Net;

namespace Api.Tests.Llm;

public sealed class FakeHttpMessageHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public List<string> ApiKeys { get; } = [];
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        ApiKeys.Add(request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : "");
        Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        return await responder(CallCount, cancellationToken);
    }
}
