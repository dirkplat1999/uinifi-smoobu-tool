namespace UnifiSmoobuTool.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
