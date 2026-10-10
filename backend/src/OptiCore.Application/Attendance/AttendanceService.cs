using OptiCore.Application.Employees;
using OptiCore.Domain.Attendance;

namespace OptiCore.Application.Attendance;

public sealed class AttendanceService(IAttendanceRepository attendance, IEmployeeRepository employees,
    TimeProvider clock, AttendanceMidnightPolicy midnight) : IAttendanceService
{
    public Task<AttendanceDto> CheckInAsync(int employeeNumber, Guid actor, CancellationToken ct) =>
        attendance.ExecuteExclusiveAsync(async () =>
        {
            await RequireEmployeeAsync(actor, ct);
            var employee = await employees.GetByNumberAsync(employeeNumber, false, ct) ?? throw new EmployeeNotFoundException();
            if (!employee.IsActive) throw new InactiveAttendanceEmployeeException();
            var now = clock.GetUtcNow();
            var open = await attendance.GetOpenAsync(employee.Id, ct);
            if (open is not null && !CloseOverdue(open, now)) throw new DuplicateAttendanceException();
            // Persist closure first so the filtered unique index permits the new session.
            if (open is not null) await attendance.SaveChangesAsync(ct);
            var record = new AttendanceRecord(employee.Id, actor, now, midnight.NextMidnightUtc(now));
            await attendance.AddAsync(record, ct);
            await attendance.SaveChangesAsync(ct);
            return AttendanceDto.From(record, employeeNumber);
        }, ct);

    public async Task<AttendanceDto> CheckOutAsync(int employeeNumber, Guid actor, CancellationToken ct)
    {
        var result = await attendance.ExecuteExclusiveAsync<AttendanceDto?>(async () =>
        {
            await RequireEmployeeAsync(actor, ct);
            var employee = await employees.GetByNumberAsync(employeeNumber, false, ct) ?? throw new EmployeeNotFoundException();
            var open = await attendance.GetOpenAsync(employee.Id, ct);
            if (open is null) return null;
            var now = clock.GetUtcNow();
            var automatic = CloseOverdue(open, now);
            if (!automatic) open.CheckOut(now, now, actor);
            await attendance.SaveChangesAsync(ct);
            return automatic ? null : AttendanceDto.From(open, employeeNumber);
        }, ct);
        // Recovery must commit even when there is no current-day session to check out.
        return result ?? throw new NoOpenAttendanceException();
    }

    public Task<int> RecoverAsync(CancellationToken ct) => attendance.ExecuteExclusiveAsync(async () =>
    {
        var now = clock.GetUtcNow();
        var overdue = await attendance.GetOverdueAsync(now, ct);
        foreach (var row in overdue) CloseOverdue(row, now);
        await attendance.SaveChangesAsync(ct);
        return overdue.Count;
    }, ct);

    private static bool CloseOverdue(AttendanceRecord row, DateTimeOffset now)
    {
        if (row.AutomaticCheckoutDueAtUtc > now) return false;
        row.CheckOut(row.AutomaticCheckoutDueAtUtc, now, null);
        return true;
    }

    public async Task<AttendanceStatusDto> StatusAsync(int employeeNumber, Guid actor, CancellationToken ct)
    {
        await RequireEmployeeAsync(actor, ct);
        var employee = await employees.GetByNumberAsync(employeeNumber, false, ct) ?? throw new EmployeeNotFoundException();
        var open = await attendance.GetOpenAsync(employee.Id, ct);
        // An overdue session is effectively closed at midnight even before recovery persists it.
        var current = open is not null && open.AutomaticCheckoutDueAtUtc > clock.GetUtcNow();
        return new(employee.EmployeeNumber, employee.FirstName, employee.LastName, current, current ? open!.CheckInAtUtc : null);
    }

    public async Task<AttendanceHistoryPage> HistoryAsync(int? employeeNumber, DateOnly? from, DateOnly? to,
        int page, int pageSize, Guid actor, CancellationToken ct)
    {
        await RequireManagerAsync(actor, ct);
        if (employeeNumber <= 0 || page < 1 || page > int.MaxValue / 100 || pageSize is < 1 or > 100 || from > to || to == DateOnly.MaxValue)
            throw new ArgumentException("מסנני הנוכחות אינם תקינים.");
        return await attendance.HistoryAsync(employeeNumber, from is null ? null : midnight.StartOfDayUtc(from.Value),
            to is null ? null : midnight.StartOfDayUtc(to.Value.AddDays(1)), page, pageSize, ct);
    }

    public async Task<AttendanceDetailsDto> DetailsAsync(Guid id, Guid actor, CancellationToken ct)
    {
        await RequireManagerAsync(actor, ct);
        return await attendance.DetailsAsync(id, ct) ?? throw new AttendanceNotFoundException();
    }

    public Task<AttendanceDetailsDto> CorrectAsync(Guid id, CorrectAttendanceRequest request, Guid actor, CancellationToken ct) =>
        attendance.ExecuteExclusiveAsync(async () =>
        {
            await RequireManagerAsync(actor, ct);
            var row = await attendance.GetByIdAsync(id, ct) ?? throw new AttendanceNotFoundException();
            if (request.ExpectedCheckInAtUtc != row.CheckInAtUtc || request.ExpectedCheckOutAtUtc != row.CheckOutAtUtc ||
                request.ExpectedUpdatedAtUtc != row.UpdatedAtUtc)
                throw new StaleAttendanceException();
            var correction = row.Correct(request.CheckInAtUtc, request.CheckOutAtUtc,
                midnight.NextMidnightUtc(request.CheckInAtUtc), actor, clock.GetUtcNow(), request.Reason);
            await attendance.AddCorrectionAsync(correction, ct);
            // Record and audit are saved and committed together by ExecuteExclusiveAsync.
            await attendance.SaveChangesAsync(ct);
            return await attendance.DetailsAsync(id, ct) ?? throw new AttendanceNotFoundException();
        }, ct);

    private async Task<OptiCore.Domain.Employees.Employee> RequireEmployeeAsync(Guid actor, CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(actor, false, ct) ?? throw new InvalidCredentialsException();
        if (!employee.IsActive) throw new InactiveEmployeeException();
        return employee;
    }

    private async Task RequireManagerAsync(Guid actor, CancellationToken ct)
    {
        var manager = await RequireEmployeeAsync(actor, ct);
        if (!manager.IsManager) throw new ManagerRequiredException();
    }
}
