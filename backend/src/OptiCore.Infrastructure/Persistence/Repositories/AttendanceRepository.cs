using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OptiCore.Application.Attendance;
using OptiCore.Domain.Attendance;

namespace OptiCore.Infrastructure.Persistence.Repositories;

public sealed class AttendanceRepository(OptiCoreDbContext db) : IAttendanceRepository
{
    public async Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Stable actor/subject status against existing Employee writers. Always lock in this order.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Employees\" IN SHARE MODE", ct);
        // Serialize attendance writers across API instances, including recovery workers.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"AttendanceRecords\" IN SHARE ROW EXCLUSIVE MODE", ct);
        var result = await operation();
        await transaction.CommitAsync(ct);
        return result;
    }

    public Task<AttendanceRecord?> GetOpenAsync(Guid employeeId, CancellationToken ct) =>
        db.AttendanceRecords.SingleOrDefaultAsync(row => row.EmployeeId == employeeId && row.CheckOutAtUtc == null, ct);

    public async Task<IReadOnlyList<AttendanceRecord>> GetOverdueAsync(DateTimeOffset now, CancellationToken ct) =>
        await db.AttendanceRecords.Where(row => row.CheckOutAtUtc == null && row.AutomaticCheckoutDueAtUtc <= now).ToListAsync(ct);

    public async Task AddAsync(AttendanceRecord record, CancellationToken ct) => await db.AttendanceRecords.AddAsync(record, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_AttendanceRecords_EmployeeId_Open" })
        { throw new DuplicateAttendanceException(); }
    }
}
