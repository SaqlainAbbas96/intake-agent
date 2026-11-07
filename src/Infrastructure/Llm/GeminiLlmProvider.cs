using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using Core.Llm;

namespace Infrastructure.Llm;

public sealed class GeminiLlmProvider(HttpClient http, GeminiOptions options) : ILlmProvider
{
    public string Name => "gemini";

    /// <summary>Waits between retries. Tests replace this so they run instantly.</summary>
    public Func<TimeSpan, CancellationToken, Task> DelayAsync { get; init; } =
        (delay, cancellationToken) => Task.Delay(delay, cancellationToken);

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = BuildBody(request);
        var path = $"v1beta/models/{Uri.EscapeDataString(options.Model)}:generateContent";
        var stopwatch = Stopwatch.StartNew();
        LlmProviderException? lastError = null;
        TimeSpan? retryAfter = null;

        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            if (attempt > 0)
                await DelayAsync(GetBackoff(attempt, retryAfter), cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
                {
                    Content = JsonContent.Create(body)
                };
                httpRequest.Headers.Add("x-goog-api-key", options.ApiKey);

                using var response = await http.SendAsync(httpRequest, timeout.Token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(timeout.Token);
                    return ParseResponse(json, stopwatch.Elapsed);
                }

                var errorBody = await response.Content.ReadAsStringAsync(timeout.Token);
                lastError = new LlmProviderException(
                    $"Gemini returned HTTP {(int)response.StatusCode}: {Truncate(errorBody)}");

                if (!IsRetryable(response.StatusCode))
                    throw lastError;

                retryAfter = response.Headers.RetryAfter?.Delta;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // the caller cancelled, so do not retry
            }
            catch (OperationCanceledException ex)
            {
                lastError = new LlmProviderException(
                    $"Gemini request timed out after {options.Timeout.TotalSeconds:0.##}s.", ex);
                retryAfter = null;
            }
            catch (HttpRequestException ex)
            {
                lastError = new LlmProviderException("Could not reach Gemini.", ex);
                retryAfter = null;
            }
        }

        throw new LlmProviderException(
            $"Gemini call failed after {options.MaxRetries + 1} attempts.", lastError!);
    }

    private JsonObject BuildBody(LlmRequest request)
    {
        JsonNode? schema;
        try
        {
            schema = JsonNode.Parse(request.JsonSchema);
        }
        catch (JsonException ex)
        {
            throw new LlmProviderException("The JSON schema is not valid JSON.", ex);
        }

        return new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = request.Prompt })
            }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0,
                ["maxOutputTokens"] = options.MaxOutputTokens,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = schema
            }
        };
    }

    private LlmResponse ParseResponse(string json, TimeSpan latency)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("promptFeedback", out var feedback) &&
                feedback.TryGetProperty("blockReason", out var reason))
                throw new LlmProviderException($"Gemini blocked the prompt: {reason.GetString()}");

            var candidate = root.GetProperty("candidates")[0];

            if (candidate.TryGetProperty("finishReason", out var finish) && finish.GetString() == "MAX_TOKENS")
                throw new LlmProviderException(
                    "Gemini output was cut off (MAX_TOKENS). Raise MaxOutputTokens or shorten the input.");

            var text = candidate.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text))
                throw new LlmProviderException("Gemini returned no text.");

            return new LlmResponse(text, ReadUsage(root), latency, Name, options.Model);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException
                                       or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new LlmProviderException("Unexpected response shape from Gemini.", ex);
        }
    }

    private static LlmUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage))
            return new LlmUsage(0, 0);

        int Read(string name) => usage.TryGetProperty(name, out var value) && value.TryGetInt32(out var n) ? n : 0;

        // "thinking" tokens are billed as output, so count them as completion tokens
        return new LlmUsage(Read("promptTokenCount"), Read("candidatesTokenCount") + Read("thoughtsTokenCount"));
    }

    private TimeSpan GetBackoff(int attempt, TimeSpan? retryAfter)
    {
        var exponential = TimeSpan.FromMilliseconds(options.InitialBackoff.TotalMilliseconds * Math.Pow(2, attempt - 1));
        var capped = exponential > options.MaxBackoff ? options.MaxBackoff : exponential;
        var delay = capped + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)); // jitter
        return retryAfter is { } serverDelay && serverDelay > delay ? serverDelay : delay;
    }

    private static bool IsRetryable(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300] + "...";
}
