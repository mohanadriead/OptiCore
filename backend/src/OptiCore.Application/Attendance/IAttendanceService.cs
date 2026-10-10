namespace OptiCore.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceDto> CheckInAsync(int employeeNumber, Guid actor, CancellationToken cancellationToken);
    Task<AttendanceDto> CheckOutAsync(int employeeNumber, Guid actor, CancellationToken cancellationToken);
    Task<int> RecoverAsync(CancellationToken cancellationToken);
}
