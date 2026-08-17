namespace UnifiSmoobuTool.Infrastructure.Smoobu;

public sealed class SmoobuApiException : Exception
{
    public int? StatusCode { get; }

    public SmoobuApiException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
