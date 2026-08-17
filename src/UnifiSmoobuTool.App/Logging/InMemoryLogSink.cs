using System.Collections.ObjectModel;
using Serilog.Core;
using Serilog.Events;

namespace UnifiSmoobuTool.App.Logging;

public sealed class LogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public string? Exception { get; init; }
}

/// <summary>Feeds the in-app Log Viewer (Feature 8) with the most recent log events, in addition
/// to the rolling file sink used for on-disk troubleshooting.</summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private const int MaxEntries = 2000;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public void Emit(LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString(),
        };

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        });
    }
}
