namespace OptiCore.Application.Permissions;

public interface IEmployeePermissionRepository
{
    Task<IReadOnlyList<string>> GetAssignedAsync(Guid employeeId, CancellationToken cancellationToken);
    // Called inside IEmployeeRepository.ExecuteExclusiveAsync so role validation and
    // assignment replacement share the same transaction/employee-write lock.
    Task ReplaceAsync(Guid employeeId, IReadOnlyList<string> permissions, CancellationToken cancellationToken);
}
