using UnifiSmoobuTool.Core.Abstractions;

namespace UnifiSmoobuTool.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
