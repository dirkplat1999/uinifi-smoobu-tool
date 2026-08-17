using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnifiSmoobuTool.Infrastructure.Tests;

/// <summary>A minimal loopback HTTP server for exercising API clients that build their own
/// internal HttpClient (so a FakeHttpMessageHandler can't be injected). Plain HTTP is sufficient
/// since certificate trust is a separate, already-tested concern.</summary>
internal sealed class TestHttpServer : IDisposable
{
    public sealed class RecordedRequest
    {
        public required string Method { get; init; }
        public required string Path { get; init; }
        public required Dictionary<string, string> Headers { get; init; }
        public required string Body { get; init; }
    }

    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loopTask;

    public List<RecordedRequest> Requests { get; } = new();
    public (int StatusCode, string Body) NextResponse { get; set; } = (200, """{"code":"SUCCESS","data":null,"msg":"success"}""");
    public string BaseUrl { get; }

    private TestHttpServer(HttpListener listener, int port)
    {
        _listener = listener;
        BaseUrl = $"http://127.0.0.1:{port}";
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public static Task<TestHttpServer> StartAsync()
    {
        var port = GetFreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return Task.FromResult(new TestHttpServer(listener, port));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
            }

            Requests.Add(new RecordedRequest
            {
                Method = context.Request.HttpMethod,
                Path = context.Request.Url!.AbsolutePath,
                Headers = context.Request.Headers.AllKeys
                    .Where(k => k is not null)
                    .ToDictionary(k => k!, k => context.Request.Headers[k] ?? ""),
                Body = body,
            });

            var (statusCode, responseBody) = NextResponse;
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes(responseBody);
            await context.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
    }
}
