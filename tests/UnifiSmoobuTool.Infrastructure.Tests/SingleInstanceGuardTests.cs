using UnifiSmoobuTool.Infrastructure.Startup;
using Xunit;

namespace UnifiSmoobuTool.Infrastructure.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuard_ReportsNotFirstInstance_WhileFirstIsStillHeld()
    {
        using var first = new SingleInstanceGuard();
        Assert.True(first.IsFirstInstance);

        using var second = new SingleInstanceGuard();
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void NewGuard_IsFirstInstance_AfterThePreviousOneWasDisposed()
    {
        using (var first = new SingleInstanceGuard())
        {
            Assert.True(first.IsFirstInstance);
        }

        using var second = new SingleInstanceGuard();
        Assert.True(second.IsFirstInstance);
    }
}
