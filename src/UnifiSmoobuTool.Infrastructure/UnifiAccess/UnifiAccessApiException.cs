namespace UnifiSmoobuTool.Infrastructure.UnifiAccess;

public sealed class UnifiAccessApiException : Exception
{
    public int? StatusCode { get; }
    public string? ApiCode { get; }

    public UnifiAccessApiException(string message, int? statusCode = null, string? apiCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ApiCode = apiCode;
    }
}
