namespace UnifiSmoobuTool.Core.Services;

/// <summary>
/// Computes the UniFi Access visitor validity window: active from 01:00 local time on the arrival
/// date, until midnight local time at the start of the day after departure.
/// </summary>
public static class AccessWindowCalculator
{
    public static (DateTimeOffset Start, DateTimeOffset End) Calculate(
        DateOnly arrival,
        DateOnly departure,
        TimeZoneInfo localTimeZone)
    {
        ArgumentNullException.ThrowIfNull(localTimeZone);

        var startLocal = new DateTime(arrival.Year, arrival.Month, arrival.Day, 1, 0, 0, DateTimeKind.Unspecified);

        var endDate = departure.AddDays(1);
        var endLocal = new DateTime(endDate.Year, endDate.Month, endDate.Day, 0, 0, 0, DateTimeKind.Unspecified);

        var start = new DateTimeOffset(startLocal, localTimeZone.GetUtcOffset(startLocal));
        var end = new DateTimeOffset(endLocal, localTimeZone.GetUtcOffset(endLocal));

        return (start, end);
    }
}
