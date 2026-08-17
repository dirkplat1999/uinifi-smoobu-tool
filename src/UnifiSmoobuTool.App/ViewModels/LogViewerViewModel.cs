using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.App.Logging;

namespace UnifiSmoobuTool.App.ViewModels;

/// <summary>Backs the Log Viewer screen (Feature 8): a live-tailing, filterable view over the
/// same log events written to the on-disk rolling log file, for troubleshooting API interactions.</summary>
public sealed partial class LogViewerViewModel : ObservableObject
{
    private readonly InMemoryLogSink _sink;

    [ObservableProperty]
    private string _filterText = "";

    public ICollectionView Entries { get; }

    public LogViewerViewModel(InMemoryLogSink sink)
    {
        _sink = sink;
        Entries = CollectionViewSource.GetDefaultView(_sink.Entries);
        Entries.Filter = FilterPredicate;
    }

    partial void OnFilterTextChanged(string value) => Entries.Refresh();

    private bool FilterPredicate(object obj)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        return obj is LogEntry entry &&
            (entry.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
             entry.Level.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void Clear() => _sink.Entries.Clear();
}
