using UnifiSmoobuTool.Core.Services;
using Xunit;

namespace UnifiSmoobuTool.Core.Tests;

public class AccessWindowCalculatorTests
{
    [Fact]
    public void Calculate_StartsAt0100OnArrival_EndsAtMidnightDayAfterDeparture()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var arrival = new DateOnly(2026, 8, 20);
        var departure = new DateOnly(2026, 8, 24);

        var (start, end) = AccessWindowCalculator.Calculate(arrival, departure, tz);

        Assert.Equal(new DateTime(2026, 8, 20, 1, 0, 0), start.LocalDateTime, TimeSpan.FromSeconds(1));
        Assert.Equal(1, start.LocalDateTime.Hour);
        Assert.Equal(0, start.LocalDateTime.Minute);

        Assert.Equal(new DateTime(2026, 8, 25, 0, 0, 0), end.LocalDateTime, TimeSpan.FromSeconds(1));
        Assert.True(end > start);
    }

    [Fact]
    public void Calculate_HandlesMonthBoundaryOnDeparturePlusOne()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var arrival = new DateOnly(2026, 1, 29);
        var departure = new DateOnly(2026, 1, 31);

        var (_, end) = AccessWindowCalculator.Calculate(arrival, departure, tz);

        Assert.Equal(2, end.Month);
        Assert.Equal(1, end.Day);
    }
}
