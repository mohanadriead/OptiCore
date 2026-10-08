using OptiCore.Application.Permissions;
using OptiCore.Tests.Employees;

namespace OptiCore.Tests.Permissions;

internal sealed class FakeEmployeePermissionRepository(FakeEmployeeRepository employees) : IEmployeePermissionRepository
{
    private readonly Dictionary<Guid, string[]> assignments = [];
    public Task<IReadOnlyList<string>> GetAssignedAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(assignments.GetValueOrDefault(employeeId, []));
    public Task ReplaceAsync(Guid employeeId, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
    {
        Assert.True(employees.InTransaction);
        assignments[employeeId] = permissions.ToArray();
        return Task.CompletedTask;
    }
}
