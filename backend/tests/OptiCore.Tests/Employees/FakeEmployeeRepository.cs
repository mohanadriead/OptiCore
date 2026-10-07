using OptiCore.Application.Employees;
using OptiCore.Domain.Employees;

namespace OptiCore.Tests.Employees;

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    public List<Employee> Employees { get; } = [];
    private readonly SemaphoreSlim gate = new(1);
    public bool InTransaction { get; private set; }

    public Employee Seed(string username = "manager", bool manager = true, string nationalId = "123456789", string? hash = null)
    {
        var employee = new Employee(" First ", " Last ", username, hash ?? new OptiCore.Infrastructure.Security.EmployeePasswordHasher().Hash("Test-password-123"),
            "0501234567", nationalId, manager, null);
        typeof(Employee).GetProperty(nameof(Employee.EmployeeNumber))!.SetValue(employee, Employees.Count + 1);
        Employees.Add(employee);
        return employee;
    }

    public Task AddAsync(Employee employee, CancellationToken ct) { Employees.Add(employee); return Task.CompletedTask; }
    public Task<Employee?> GetByNumberAsync(int number, bool forUpdate, CancellationToken ct) => Task.FromResult(Employees.SingleOrDefault(e => e.EmployeeNumber == number));
    public Task<Employee?> GetByIdAsync(Guid id, bool forUpdate, CancellationToken ct) => Task.FromResult(Employees.SingleOrDefault(e => e.Id == id));
    public Task<Employee?> GetByUsernameAsync(string username, CancellationToken ct) => Task.FromResult(Employees.SingleOrDefault(e => e.NormalizedUsername == username));
    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) => Task.FromResult(Employees.Any(e => e.NormalizedUsername == username));
    public Task<bool> NationalIdExistsAsync(string id, CancellationToken ct) => Task.FromResult(Employees.Any(e => e.NationalId == id));
    public Task<bool> AnyAsync(CancellationToken ct) => Task.FromResult(Employees.Count > 0);
    public Task<IReadOnlyList<Employee>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Employee>>(Employees.ToArray());
    public Task<int> CountActiveManagersAsync(CancellationToken ct)
    {
        Assert.True(InTransaction);
        return Task.FromResult(Employees.Count(e => e.IsActive && e.IsManager));
    }
    public Task SaveChangesAsync(CancellationToken ct)
    {
        Assert.True(InTransaction);
        foreach (var employee in Employees.Where(e => e.EmployeeNumber == 0))
            typeof(Employee).GetProperty(nameof(Employee.EmployeeNumber))!.SetValue(employee, Employees.Max(e => e.EmployeeNumber) + 1);
        return Task.CompletedTask;
    }
    public async Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        InTransaction = true;
        try { return await action(); }
        finally { InTransaction = false; gate.Release(); }
    }
}
