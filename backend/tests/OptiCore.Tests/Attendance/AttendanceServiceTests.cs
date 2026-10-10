using OptiCore.Application.Attendance;
using OptiCore.Application.Employees;
using OptiCore.Domain.Attendance;
using OptiCore.Tests.Employees;

namespace OptiCore.Tests.Attendance;

public sealed class AttendanceServiceTests
{
    private readonly FakeEmployeeRepository employees = new();
    private readonly FakeAttendanceRepository records = new();
    private readonly TestClock clock = new(DateTimeOffset.Parse("2026-07-01T10:00:00Z"));
    private AttendanceService Service => new(records, employees, clock, new());

    [Fact]
    public async Task SubjectUsesNumberAndManagerIsOnlyAuditActor()
    {
        var actor = employees.Seed();
        var subject = employees.Seed("subject", false, "222222222");
        var result = await Service.CheckInAsync(subject.EmployeeNumber, actor.Id, default);
        Assert.Equal(subject.Id, result.EmployeeId);
        Assert.Equal(actor.Id, result.CreatedByEmployeeId);
        Assert.Equal(clock.Now, result.CheckInAtUtc);
        clock.Now = clock.Now.AddHours(1);
        subject.Deactivate(actor.Id);
        var closed = await Service.CheckOutAsync(subject.EmployeeNumber, actor.Id, default);
        Assert.Equal(actor.Id, closed.UpdatedByEmployeeId);
        Assert.Equal(clock.Now, closed.CheckOutAtUtc);
        Assert.Equal(clock.Now, closed.CheckoutProcessedAtUtc);
        Assert.False(closed.WasCheckoutAutomatic);
    }

    [Fact]
    public async Task RejectsInactiveSubjectNonManagerAndMissingNumber()
    {
        var actor = employees.Seed();
        var subject = employees.Seed("subject", false, "222222222");
        await Assert.ThrowsAsync<ManagerRequiredException>(() => Service.CheckInAsync(actor.EmployeeNumber, subject.Id, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => Service.CheckOutAsync(actor.EmployeeNumber, subject.Id, default));
        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => Service.CheckInAsync(999, actor.Id, default));
        subject.Deactivate(actor.Id);
        await Assert.ThrowsAsync<InactiveAttendanceEmployeeException>(() => Service.CheckInAsync(subject.EmployeeNumber, actor.Id, default));
        Assert.Empty(records.Rows);
    }

    [Fact]
    public async Task DuplicateAndMissingSessionAreConflicts()
    {
        var actor = employees.Seed();
        await Assert.ThrowsAsync<NoOpenAttendanceException>(() => Service.CheckOutAsync(1, actor.Id, default));
        await Service.CheckInAsync(1, actor.Id, default);
        await Assert.ThrowsAsync<DuplicateAttendanceException>(() => Service.CheckInAsync(1, actor.Id, default));
        Assert.Single(records.Rows);
    }

    [Fact]
    public async Task DowntimeRecoveryUsesOriginalMidnightAndIsIdempotent()
    {
        var actor = employees.Seed();
        await Service.CheckInAsync(1, actor.Id, default);
        var row = Assert.Single(records.Rows);
        Assert.Equal(DateTimeOffset.Parse("2026-07-01T21:00:00Z"), row.AutomaticCheckoutDueAtUtc);
        clock.Now = clock.Now.AddDays(3);
        Assert.Equal(1, await Service.RecoverAsync(default));
        Assert.Equal(0, await Service.RecoverAsync(default));
        Assert.Equal(row.AutomaticCheckoutDueAtUtc, row.CheckOutAtUtc);
        Assert.Equal(clock.Now, row.CheckoutProcessedAtUtc);
        Assert.Equal(clock.Now, row.UpdatedAtUtc);
        Assert.True(row.WasCheckoutAutomatic);
        Assert.Null(row.UpdatedByEmployeeId);
    }

    [Fact]
    public async Task CheckoutAtBoundaryRecoversBeforeReturningConflictAndNextCheckInSucceeds()
    {
        var actor = employees.Seed();
        await Service.CheckInAsync(1, actor.Id, default);
        clock.Now = records.Rows[0].AutomaticCheckoutDueAtUtc;
        await Assert.ThrowsAsync<NoOpenAttendanceException>(() => Service.CheckOutAsync(1, actor.Id, default));
        Assert.True(records.Rows[0].WasCheckoutAutomatic);
        await Service.CheckInAsync(1, actor.Id, default);
        Assert.Equal(2, records.Rows.Count);
        Assert.Single(records.Rows, row => row.CheckOutAtUtc is null);
    }

    [Fact]
    public async Task CheckInRecoversOverdueSessionWithoutWorker()
    {
        var actor = employees.Seed();
        await Service.CheckInAsync(1, actor.Id, default);
        clock.Now = clock.Now.AddDays(2);
        await Service.CheckInAsync(1, actor.Id, default);
        Assert.True(records.Rows[0].WasCheckoutAutomatic);
        Assert.Single(records.Rows, row => row.CheckOutAtUtc is null);
    }

    [Theory]
    [InlineData("2026-01-01T10:00:00Z", "2026-01-01T22:00:00Z")]
    [InlineData("2026-07-01T10:00:00Z", "2026-07-01T21:00:00Z")]
    [InlineData("2026-03-26T22:00:00Z", "2026-03-27T21:00:00Z")]
    [InlineData("2026-10-24T21:00:00Z", "2026-10-25T22:00:00Z")]
    public void MidnightUsesJerusalemDst(string start, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), new AttendanceMidnightPolicy().NextMidnightUtc(DateTimeOffset.Parse(start)));

    [Fact]
    public async Task ConcurrentCheckInsCreateOnlyOneSessionAndCheckoutsCloseOnlyOnce()
    {
        var actor = employees.Seed();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await Service.CheckInAsync(1, actor.Id, default); return true; }
            catch (DuplicateAttendanceException) { return false; }
        }));
        Assert.Single(outcomes, success => success);
        var exits = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await Service.CheckOutAsync(1, actor.Id, default); return true; }
            catch (NoOpenAttendanceException) { return false; }
        }));
        Assert.Single(exits, success => success);
    }
}

internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeAttendanceRepository : IAttendanceRepository
{
    public List<AttendanceRecord> Rows { get; } = [];
    private readonly SemaphoreSlim gate = new(1);
    public async Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return await operation(); }
        finally { gate.Release(); }
    }
    public Task<AttendanceRecord?> GetOpenAsync(Guid id, CancellationToken ct) => Task.FromResult(Rows.SingleOrDefault(row => row.EmployeeId == id && row.CheckOutAtUtc is null));
    public Task<IReadOnlyList<AttendanceRecord>> GetOverdueAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult<IReadOnlyList<AttendanceRecord>>(Rows.Where(row => row.CheckOutAtUtc is null && row.AutomaticCheckoutDueAtUtc <= now).ToArray());
    public Task AddAsync(AttendanceRecord row, CancellationToken ct) { Rows.Add(row); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
