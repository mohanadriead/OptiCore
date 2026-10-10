namespace OptiCore.Application.Attendance;

public sealed class AttendanceMidnightPolicy
{
    private readonly TimeZoneInfo storeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    public DateTimeOffset StartOfDayUtc(DateOnly day) => new(
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), storeZone), TimeSpan.Zero);

    public DateTimeOffset NextMidnightUtc(DateTimeOffset checkIn)
    {
        var local = TimeZoneInfo.ConvertTime(checkIn, storeZone);
        var midnight = DateTime.SpecifyKind(local.Date.AddDays(1), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(midnight, storeZone), TimeSpan.Zero);
    }
}
