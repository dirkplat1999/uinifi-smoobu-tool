using System.Text;

namespace UnifiSmoobuTool.Infrastructure.Tests;

/// <summary>Records every outgoing request and replies with a queued (or default) response, so
/// API clients can be tested without a live server.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public sealed class RecordedRequest
    {
        public required HttpMethod Method { get; init; }
        public required Uri Uri { get; init; }
        public required Dictionary<string, string> Headers { get; init; }
        public required string Body { get; init; }
    }

    public List<RecordedRequest> Requests { get; } = new();
    public Queue<(int StatusCode, string Body)> Responses { get; } = new();
    public (int StatusCode, string Body) DefaultResponse { get; set; } = (200, "{\"code\":\"SUCCESS\",\"data\":null,\"msg\":\"success\"}");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        Requests.Add(new RecordedRequest
        {
            Method = request.Method,
            Uri = request.RequestUri!,
            Headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            Body = body,
        });

        var (statusCode, responseBody) = Responses.Count > 0 ? Responses.Dequeue() : DefaultResponse;

        return new HttpResponseMessage((System.Net.HttpStatusCode)statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
    }
}
