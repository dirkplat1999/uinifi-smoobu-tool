using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Tests;

internal sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    public AppSettings Settings { get; set; } = new();

    public Task<AppSettings> GetAsync(CancellationToken ct = default) => Task.FromResult(Settings);

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}
