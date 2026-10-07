using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OptiCore.Application.Employees;
using OptiCore.Domain.Employees;

namespace OptiCore.Infrastructure.Persistence.Repositories;

public sealed class EmployeeRepository(OptiCoreDbContext db) : IEmployeeRepository
{
    public async Task AddAsync(Employee employee, CancellationToken cancellationToken) =>
        await db.Employees.AddAsync(employee, cancellationToken);

    public Task<Employee?> GetByNumberAsync(int number, bool forUpdate, CancellationToken cancellationToken) =>
        Query(forUpdate).SingleOrDefaultAsync(employee => employee.EmployeeNumber == number, cancellationToken);

    public Task<Employee?> GetByIdAsync(Guid id, bool forUpdate, CancellationToken cancellationToken) =>
        Query(forUpdate).SingleOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public Task<Employee?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        Query(false).SingleOrDefaultAsync(employee => employee.NormalizedUsername == normalizedUsername, cancellationToken);

    public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(employee => employee.NormalizedUsername == normalizedUsername, cancellationToken);

    public Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(employee => employee.NationalId == nationalId, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken) => db.Employees.AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<Employee>> ListAsync(CancellationToken cancellationToken) =>
        await Query(false).OrderBy(employee => employee.EmployeeNumber).ToListAsync(cancellationToken);

    public Task<int> CountActiveManagersAsync(CancellationToken cancellationToken) =>
        db.Employees.CountAsync(employee => employee.IsActive && employee.IsManager, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Employees_NormalizedUsername" })
        { throw new DuplicateUsernameException(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Employees_NationalId" })
        { throw new DuplicateEmployeeNationalIdException(); }
    }

    public async Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Conflicts with itself and all writes, but permits ordinary reads. Also protects an empty table at bootstrap.
        // All employee writes acquire this lock BEFORE loading any tracked employee or counting managers.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Employees\" IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        var result = await operation();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private IQueryable<Employee> Query(bool forUpdate) => forUpdate ? db.Employees.AsTracking() : db.Employees.AsNoTracking();
}
