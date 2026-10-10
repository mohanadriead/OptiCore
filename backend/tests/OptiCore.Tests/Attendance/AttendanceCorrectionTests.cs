using OptiCore.Application.Attendance;
using OptiCore.Application.Employees;
using OptiCore.Tests.Employees;

namespace OptiCore.Tests.Attendance;

public sealed class AttendanceCorrectionTests
{
    private readonly FakeEmployeeRepository employees = new();
    private readonly TestClock clock = new(DateTimeOffset.Parse("2026-07-01T10:00:00Z"));

    private async Task<(AttendanceService Service, FakeAttendanceRepository Records, Guid Manager, int Subject)> Open()
    {
        var manager = employees.Seed();
        var subject = employees.Seed("subject", false, "222222222");
        var records = new FakeAttendanceRepository(employees);
        var service = new AttendanceService(records, employees, clock, new());
        await service.CheckInAsync(subject.EmployeeNumber, subject.Id, default);
        clock.Now = clock.Now.AddHours(2);
        return (service, records, manager.Id, subject.EmployeeNumber);
    }

    private static CorrectAttendanceRequest Request(OptiCore.Domain.Attendance.AttendanceRecord row,
        DateTimeOffset start, DateTimeOffset? end, string reason = "תיקון דיווח") =>
        new(start, end, reason, row.CheckInAtUtc, row.CheckOutAtUtc, row.UpdatedAtUtc);

    [Fact]
    public async Task StatusContractContainsOnlyEmployeeNumberNameAndCurrentAttendance()
    {
        var (service, _, manager, subject) = await Open();
        var status = await service.StatusAsync(subject, manager, default);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(status, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "checkInAtUtc", "employeeNumber", "firstName", "hasOpenAttendance", "lastName" },
            json.EnumerateObject().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task RegularEmployeeCanManageSelfButCannotResolveAnotherEmployee()
    {
        var (service, records, manager, subjectNumber) = await Open();
        var subject = employees.Employees[1];
        foreach (var number in new[] { 1, 999 })
        {
            await Assert.ThrowsAsync<ManagerRequiredException>(() => service.StatusAsync(number, subject.Id, default));
            await Assert.ThrowsAsync<ManagerRequiredException>(() => service.CheckInAsync(number, subject.Id, default));
            await Assert.ThrowsAsync<ManagerRequiredException>(() => service.CheckOutAsync(number, subject.Id, default));
        }
        Assert.True((await service.SelfStatusAsync(subject.Id, default)).HasOpenAttendance);
        var checkOut = await service.SelfCheckOutAsync(subject.Id, default);
        Assert.Equal(subject.Id, checkOut.UpdatedByEmployeeId);
        Assert.Equal(clock.Now, checkOut.CheckOutAtUtc);
        Assert.False((await service.StatusAsync(subjectNumber, subject.Id, default)).HasOpenAttendance);
        var checkIn = await service.SelfCheckInAsync(subject.Id, default);
        Assert.Equal(subject.Id, checkIn.EmployeeId);
        Assert.Equal(subject.Id, checkIn.CreatedByEmployeeId);
        Assert.Equal(2, records.Rows.Count);
    }

    [Fact]
    public async Task ExcessiveReasonIsRejectedBeforeAnyMutation()
    {
        var (service, records, manager, _) = await Open();
        var row = records.Rows[0];
        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(row.Id,
            Request(row, row.CheckInAtUtc, clock.Now, new string('x', 2001)), manager, default));
        Assert.Empty(records.Corrections);
        Assert.Null(row.CheckOutAtUtc);
    }

    [Fact]
    public async Task MissingCheckoutCorrectionPersistsOldNewActorReasonAndTimestampTogether()
    {
        var (service, records, manager, _) = await Open();
        var row = Assert.Single(records.Rows);
        var previous = row.CheckInAtUtc;
        var result = await service.CorrectAsync(row.Id, Request(row, previous.AddMinutes(-15), clock.Now.AddHours(-1), "  שעון לא תקין  "), manager, default);
        var audit = Assert.Single(records.Corrections);
        Assert.Equal(row.Id, audit.AttendanceRecordId);
        Assert.Equal(previous, audit.PreviousCheckInAtUtc);
        Assert.Null(audit.PreviousCheckOutAtUtc);
        Assert.Equal(previous.AddMinutes(-15), audit.NewCheckInAtUtc);
        Assert.Equal(clock.Now.AddHours(-1), audit.NewCheckOutAtUtc);
        Assert.Equal(manager, audit.CorrectedByEmployeeId);
        Assert.Equal(clock.Now, audit.CorrectedAtUtc);
        Assert.Equal("שעון לא תקין", audit.Reason);
        Assert.Equal(employees.Employees[1].Id, row.EmployeeId);
        Assert.Equal(manager, row.UpdatedByEmployeeId);
        Assert.Equal(clock.Now, row.CheckoutProcessedAtUtc);
        Assert.False(row.WasCheckoutAutomatic);
        Assert.Single(result.Corrections);
        Assert.Equal(1, result.Corrections[0].CorrectedByEmployeeNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ReasonRequiredAndFailureDoesNotChangeRecord(string reason)
    {
        var (service, records, manager, _) = await Open();
        var row = records.Rows[0];
        var before = row.CheckInAtUtc;
        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(row.Id, Request(row, before.AddMinutes(-5), clock.Now, reason), manager, default));
        Assert.Equal(before, row.CheckInAtUtc);
        Assert.Null(row.CheckOutAtUtc);
        Assert.Empty(records.Corrections);
    }

    [Fact]
    public async Task RejectsCheckoutBeforeCheckinFutureNonUtcReopeningAndBeyondMidnight()
    {
        var (service, records, manager, _) = await Open();
        var row = records.Rows[0];
        foreach (var request in new[] {
            Request(row, row.CheckInAtUtc, row.CheckInAtUtc.AddMinutes(-1)),
            Request(row, clock.Now.AddHours(1), null),
            Request(row, row.CheckInAtUtc.ToOffset(TimeSpan.FromHours(3)), clock.Now),
            Request(row, row.CheckInAtUtc, clock.Now.AddHours(1)) })
            await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(row.Id, request, manager, default));
        await service.CheckOutAsync(2, manager, default);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(row.Id, Request(row, row.CheckInAtUtc, null), manager, default));
        clock.Now = row.AutomaticCheckoutDueAtUtc.AddDays(1);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectAsync(row.Id, Request(row, row.CheckInAtUtc, row.AutomaticCheckoutDueAtUtc.AddMinutes(1)), manager, default));
        Assert.Empty(records.Corrections);
    }

    [Fact]
    public async Task RegularInactiveAndUnidentifiedActorsCannotManageAttendance()
    {
        var (service, records, manager, _) = await Open();
        var employee = employees.Employees[1];
        var row = records.Rows[0];
        await Assert.ThrowsAsync<ManagerRequiredException>(() => service.HistoryAsync(null, null, null, 1, 25, employee.Id, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => service.DetailsAsync(row.Id, employee.Id, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => service.CorrectAsync(row.Id, Request(row, row.CheckInAtUtc, clock.Now), employee.Id, default));
        employees.Employees[0].Deactivate(manager);
        await Assert.ThrowsAsync<InactiveEmployeeException>(() => service.HistoryAsync(null, null, null, 1, 25, manager, default));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.CorrectAsync(row.Id, Request(row, row.CheckInAtUtc, clock.Now), Guid.Empty, default));
        Assert.Empty(records.Corrections);
    }

    [Fact]
    public async Task StaleCorrectionCannotOverwriteCheckoutOrAnotherCorrection()
    {
        var (service, records, manager, _) = await Open();
        var row = records.Rows[0];
        var request = Request(row, row.CheckInAtUtc, clock.Now.AddMinutes(-10));
        await service.CheckOutAsync(2, manager, default);
        await Assert.ThrowsAsync<StaleAttendanceException>(() => service.CorrectAsync(row.Id, request, manager, default));
        request = Request(row, row.CheckInAtUtc, clock.Now.AddMinutes(-10));
        await service.CorrectAsync(row.Id, request, manager, default);
        await Assert.ThrowsAsync<StaleAttendanceException>(() => service.CorrectAsync(row.Id, request, manager, default));
        Assert.Single(records.Corrections);
    }

    [Fact]
    public async Task AutomaticCheckoutCorrectionRetainsBoundaryOrBecomesManual()
    {
        var (service, records, manager, _) = await Open();
        var row = records.Rows[0];
        clock.Now = row.AutomaticCheckoutDueAtUtc.AddHours(5);
        await service.RecoverAsync(default);
        var processed = row.CheckoutProcessedAtUtc;
        clock.Now = clock.Now.AddHours(1);
        await service.CorrectAsync(row.Id, Request(row, row.CheckInAtUtc.AddMinutes(-10), row.CheckOutAtUtc), manager, default);
        Assert.True(row.WasCheckoutAutomatic);
        Assert.Equal(row.AutomaticCheckoutDueAtUtc, row.CheckOutAtUtc);
        Assert.Equal(processed, row.CheckoutProcessedAtUtc);
        Assert.Equal(manager, row.UpdatedByEmployeeId);
        await service.CorrectAsync(row.Id, Request(row, row.CheckInAtUtc, row.AutomaticCheckoutDueAtUtc.AddHours(-1)), manager, default);
        Assert.False(row.WasCheckoutAutomatic);
        Assert.Equal(clock.Now, row.CheckoutProcessedAtUtc);
        Assert.Equal(2, records.Corrections.Count);
    }

    [Fact]
    public async Task CorrectingCheckinRecalculatesIsraelMidnightWithoutChangingEmployee()
    {
        var (service, records, manager, _) = await Open();
        var row = records.Rows[0];
        var employee = row.EmployeeId;
        clock.Now = clock.Now.AddDays(2);
        await service.RecoverAsync(default);
        var newStart = DateTimeOffset.Parse("2026-06-30T10:00:00Z");
        var newEnd = DateTimeOffset.Parse("2026-06-30T18:00:00Z");
        await service.CorrectAsync(row.Id, Request(row, newStart, newEnd), manager, default);
        Assert.Equal(DateTimeOffset.Parse("2026-06-30T21:00:00Z"), row.AutomaticCheckoutDueAtUtc);
        Assert.Equal(employee, row.EmployeeId);
        Assert.False(row.WasCheckoutAutomatic);
    }

    [Fact]
    public async Task StatusAndHistoryUseIsraelDateBoundariesAndPagination()
    {
        var (service, records, manager, number) = await Open();
        var status = await service.StatusAsync(number, employees.Employees[1].Id, default);
        Assert.True(status.HasOpenAttendance);
        Assert.Equal("First", status.FirstName);
        clock.Now = records.Rows[0].AutomaticCheckoutDueAtUtc;
        status = await service.StatusAsync(number, manager, default);
        Assert.False(status.HasOpenAttendance);
        Assert.Null(status.CheckInAtUtc);
        await service.CheckInAsync(number, manager, default);
        var julyOne = new DateOnly(2026, 7, 1);
        var julyTwo = julyOne.AddDays(1);
        var first = await service.HistoryAsync(number, julyOne, julyOne, 1, 25, manager, default);
        Assert.Single(first.Items);
        Assert.Equal(DateTimeOffset.Parse("2026-07-01T10:00:00Z"), first.Items[0].CheckInAtUtc);
        var second = await service.HistoryAsync(number, julyTwo, julyTwo, 1, 25, manager, default);
        Assert.Single(second.Items);
        Assert.Equal(clock.Now, second.Items[0].CheckInAtUtc);
        Assert.Empty((await service.HistoryAsync(1, julyOne, julyTwo, 1, 25, manager, default)).Items);
        var paged = await service.HistoryAsync(null, null, null, 2, 1, manager, default);
        Assert.Equal(2, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal(first.Items[0].Id, paged.Items[0].Id);
        await Assert.ThrowsAsync<ArgumentException>(() => service.HistoryAsync(null, julyTwo, julyOne, 1, 25, manager, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.HistoryAsync(null, null, null, 0, 25, manager, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.HistoryAsync(null, null, null, 1, 101, manager, default));
    }
}
