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
public sealed class AttendanceNotFoundException : Exception;
public sealed class StaleAttendanceException : Exception;

public sealed record AttendanceStatusDto(int EmployeeNumber, string FirstName, string LastName,
    bool HasOpenAttendance, DateTimeOffset? CheckInAtUtc);

public sealed record AttendanceHistoryDto(Guid Id, int EmployeeNumber, string FirstName, string LastName,
    DateTimeOffset CheckInAtUtc, DateTimeOffset? CheckOutAtUtc, bool WasCheckoutAutomatic,
    DateTimeOffset AutomaticCheckoutDueAtUtc, DateTimeOffset? CheckoutProcessedAtUtc, DateTimeOffset? UpdatedAtUtc);

public sealed record AttendanceCorrectionDto(DateTimeOffset PreviousCheckInAtUtc, DateTimeOffset? PreviousCheckOutAtUtc,
    DateTimeOffset NewCheckInAtUtc, DateTimeOffset? NewCheckOutAtUtc, DateTimeOffset CorrectedAtUtc,
    int CorrectedByEmployeeNumber, string CorrectedByFirstName, string CorrectedByLastName, string Reason);

public sealed record AttendanceDetailsDto(AttendanceHistoryDto Record, IReadOnlyList<AttendanceCorrectionDto> Corrections);
public sealed record AttendanceHistoryPage(IReadOnlyList<AttendanceHistoryDto> Items, int Total, int Page, int PageSize);

public sealed record CorrectAttendanceRequest(DateTimeOffset CheckInAtUtc, DateTimeOffset? CheckOutAtUtc,
    string Reason, DateTimeOffset ExpectedCheckInAtUtc, DateTimeOffset? ExpectedCheckOutAtUtc,
    DateTimeOffset? ExpectedUpdatedAtUtc);
