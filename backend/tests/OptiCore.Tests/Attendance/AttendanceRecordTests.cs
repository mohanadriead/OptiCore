using OptiCore.Domain.Attendance;

namespace OptiCore.Tests.Attendance;

public sealed class AttendanceRecordTests
{
    [Fact]
    public void RejectsInvalidTimesAndCannotCloseTwice()
    {
        var now = DateTimeOffset.Parse("2026-07-01T10:00:00Z");
        var actor = Guid.NewGuid();
        var row = new AttendanceRecord(Guid.NewGuid(), actor, now, now.AddHours(11));
        Assert.Throws<ArgumentException>(() => row.CheckOut(now.AddMinutes(-1), now, actor));
        Assert.Throws<ArgumentException>(() => row.CheckOut(now.AddHours(11), now.AddHours(11), actor));
        Assert.Throws<ArgumentException>(() => row.CheckOut(now, now, null));
        row.CheckOut(now.AddHours(1), now.AddHours(1), actor);
        Assert.Throws<InvalidOperationException>(() => row.CheckOut(now.AddHours(2), now.AddHours(2), actor));
        Assert.Throws<ArgumentException>(() => new AttendanceRecord(Guid.NewGuid(), actor, now, now));
        Assert.Throws<ArgumentException>(() => new AttendanceRecord(Guid.NewGuid(), actor, now.ToOffset(TimeSpan.FromHours(2)), now.AddHours(11)));
    }
}
