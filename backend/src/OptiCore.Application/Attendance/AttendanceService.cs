using OptiCore.Application.Employees;
using OptiCore.Domain.Attendance;

namespace OptiCore.Application.Attendance;

public sealed class AttendanceService(IAttendanceRepository attendance, IEmployeeRepository employees,
    TimeProvider clock, AttendanceMidnightPolicy midnight) : IAttendanceService
{
    public Task<AttendanceDto> CheckInAsync(int employeeNumber, Guid actor, CancellationToken ct) =>
        attendance.ExecuteExclusiveAsync(async () =>
        {
            await RequireManagerAsync(actor, ct);
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
            await RequireManagerAsync(actor, ct);
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

    private async Task RequireManagerAsync(Guid actor, CancellationToken ct)
    {
        var manager = await employees.GetByIdAsync(actor, false, ct) ?? throw new InvalidCredentialsException();
        if (!manager.IsActive) throw new InactiveEmployeeException();
        if (!manager.IsManager) throw new ManagerRequiredException();
    }
}
