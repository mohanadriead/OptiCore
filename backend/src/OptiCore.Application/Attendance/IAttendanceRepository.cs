using OptiCore.Domain.Attendance;

namespace OptiCore.Application.Attendance;

public interface IAttendanceRepository
{
    Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
    Task<AttendanceRecord?> GetOpenAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceRecord>> GetOverdueAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
