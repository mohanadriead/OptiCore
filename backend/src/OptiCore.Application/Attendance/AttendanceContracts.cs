using OptiCore.Domain.Attendance;

namespace OptiCore.Application.Attendance;

public sealed record AttendanceDto(Guid Id, int EmployeeNumber, Guid EmployeeId,
    DateTimeOffset CheckInAtUtc, DateTimeOffset? CheckOutAtUtc, DateTimeOffset AutomaticCheckoutDueAtUtc,
    bool WasCheckoutAutomatic, DateTimeOffset? CheckoutProcessedAtUtc,
    DateTimeOffset CreatedAtUtc, Guid? CreatedByEmployeeId, DateTimeOffset? UpdatedAtUtc, Guid? UpdatedByEmployeeId)
{
    public static AttendanceDto From(AttendanceRecord row, int number) => new(row.Id, number, row.EmployeeId,
        row.CheckInAtUtc, row.CheckOutAtUtc, row.AutomaticCheckoutDueAtUtc, row.WasCheckoutAutomatic,
        row.CheckoutProcessedAtUtc, row.CreatedAtUtc, row.CreatedByEmployeeId, row.UpdatedAtUtc, row.UpdatedByEmployeeId);
}

public sealed class DuplicateAttendanceException : Exception;
public sealed class NoOpenAttendanceException : Exception;
public sealed class InactiveAttendanceEmployeeException : Exception;
