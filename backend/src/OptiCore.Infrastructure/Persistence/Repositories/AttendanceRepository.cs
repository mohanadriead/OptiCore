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

    public Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.AttendanceRecords.SingleOrDefaultAsync(row => row.Id == id, ct);

    private IQueryable<AttendanceHistoryDto> HistoryQuery(int? employeeNumber, DateTimeOffset? from,
        DateTimeOffset? until, Guid? id, int skip, int? take)
    {
        var query = from row in db.AttendanceRecords.AsNoTracking()
            join employee in db.Employees.AsNoTracking() on row.EmployeeId equals employee.Id
            select new { Row = row, Employee = employee };
        if (employeeNumber is not null) query = query.Where(item => item.Employee.EmployeeNumber == employeeNumber);
        if (from is not null) query = query.Where(item => item.Row.CheckInAtUtc >= from);
        if (until is not null) query = query.Where(item => item.Row.CheckInAtUtc < until);
        if (id is not null) query = query.Where(item => item.Row.Id == id);
        query = query.OrderByDescending(item => item.Row.CheckInAtUtc).ThenByDescending(item => item.Row.Id);
        if (skip > 0) query = query.Skip(skip);
        if (take is not null) query = query.Take(take.Value);
        // Compose predicates and pagination before the constructor projection so EF translates
        // the whole query server-side without relying on constructor member binding.
        return query.Select(item => new AttendanceHistoryDto(item.Row.Id, item.Employee.EmployeeNumber,
            item.Employee.FirstName, item.Employee.LastName, item.Row.CheckInAtUtc, item.Row.CheckOutAtUtc,
            item.Row.WasCheckoutAutomatic, item.Row.AutomaticCheckoutDueAtUtc, item.Row.CheckoutProcessedAtUtc, item.Row.UpdatedAtUtc));
    }

    public async Task<AttendanceHistoryPage> HistoryAsync(int? employeeNumber, DateTimeOffset? from, DateTimeOffset? until,
        int page, int pageSize, CancellationToken ct)
    {
        var query = HistoryQuery(employeeNumber, from, until, null, 0, null);
        var total = await query.CountAsync(ct);
        var items = await HistoryQuery(employeeNumber, from, until, null, checked((page - 1) * pageSize), pageSize).ToListAsync(ct);
        return new(items, total, page, pageSize);
    }

    public async Task<AttendanceDetailsDto?> DetailsAsync(Guid id, CancellationToken ct)
    {
        var row = await HistoryQuery(null, null, null, id, 0, null).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var corrections = await (from correction in db.AttendanceCorrections.AsNoTracking()
            join actor in db.Employees.AsNoTracking() on correction.CorrectedByEmployeeId equals actor.Id
            where correction.AttendanceRecordId == id
            orderby correction.CorrectedAtUtc descending, correction.Id descending
            select new AttendanceCorrectionDto(correction.PreviousCheckInAtUtc, correction.PreviousCheckOutAtUtc,
                correction.NewCheckInAtUtc, correction.NewCheckOutAtUtc, correction.CorrectedAtUtc,
                actor.EmployeeNumber, actor.FirstName, actor.LastName, correction.Reason)).ToListAsync(ct);
        return new(row, corrections);
    }

    public async Task AddCorrectionAsync(AttendanceCorrection correction, CancellationToken ct) =>
        await db.AttendanceCorrections.AddAsync(correction, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_AttendanceRecords_EmployeeId_Open" })
        { throw new DuplicateAttendanceException(); }
    }
}
