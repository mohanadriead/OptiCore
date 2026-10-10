namespace OptiCore.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceStatusDto> SelfStatusAsync(Guid actor, CancellationToken cancellationToken);
    Task<AttendanceDto> SelfCheckInAsync(Guid actor, CancellationToken cancellationToken);
    Task<AttendanceDto> SelfCheckOutAsync(Guid actor, CancellationToken cancellationToken);
    Task<AttendanceDto> CheckInAsync(int employeeNumber, Guid actor, CancellationToken cancellationToken);
    Task<AttendanceDto> CheckOutAsync(int employeeNumber, Guid actor, CancellationToken cancellationToken);
    Task<int> RecoverAsync(CancellationToken cancellationToken);
    Task<AttendanceStatusDto> StatusAsync(int employeeNumber, Guid actor, CancellationToken cancellationToken);
    Task<AttendanceHistoryPage> HistoryAsync(int? employeeNumber, DateOnly? from, DateOnly? to,
        int page, int pageSize, Guid actor, CancellationToken cancellationToken);
    Task<AttendanceDetailsDto> DetailsAsync(Guid id, Guid actor, CancellationToken cancellationToken);
    Task<AttendanceDetailsDto> CorrectAsync(Guid id, CorrectAttendanceRequest request, Guid actor, CancellationToken cancellationToken);
}
