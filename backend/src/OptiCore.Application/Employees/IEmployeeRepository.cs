using OptiCore.Domain.Employees;

namespace OptiCore.Application.Employees;

public interface IEmployeeRepository
{
    Task AddAsync(Employee employee, CancellationToken cancellationToken);
    Task<Employee?> GetByNumberAsync(int number, bool forUpdate, CancellationToken cancellationToken);
    Task<Employee?> GetByIdAsync(Guid id, bool forUpdate, CancellationToken cancellationToken);
    Task<Employee?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken);
    Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken);
    Task<bool> AnyAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Employee>> ListAsync(CancellationToken cancellationToken);
    Task<int> CountActiveManagersAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    // Lock before loading employees. Commit only after the callback succeeds; otherwise roll back.
    Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
}
