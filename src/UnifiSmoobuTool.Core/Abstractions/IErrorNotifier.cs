namespace UnifiSmoobuTool.Core.Abstractions;

public interface IErrorNotifier
{
    Task NotifyErrorAsync(string component, string message, Exception? exception, CancellationToken ct = default);
}
