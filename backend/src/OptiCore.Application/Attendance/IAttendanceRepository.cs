using OptiCore.Domain.Attendance;

namespace OptiCore.Application.Attendance;

public interface IAttendanceRepository
{
    Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
    Task<AttendanceRecord?> GetOpenAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceRecord>> GetOverdueAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AttendanceHistoryPage> HistoryAsync(int? employeeNumber, DateTimeOffset? from, DateTimeOffset? until,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<AttendanceDetailsDto?> DetailsAsync(Guid id, CancellationToken cancellationToken);
    Task AddCorrectionAsync(AttendanceCorrection correction, CancellationToken cancellationToken);
}
