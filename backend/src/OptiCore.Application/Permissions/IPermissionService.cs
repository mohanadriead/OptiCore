namespace OptiCore.Application.Permissions;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionDto>> GetCatalogAsync(Guid actingManager, CancellationToken cancellationToken);
    Task<EmployeePermissionsDto> GetAssignedPermissionsAsync(int employeeNumber, Guid actingManager, CancellationToken cancellationToken);
    Task<EmployeePermissionsDto> SetAssignedPermissionsAsync(int employeeNumber, IReadOnlyList<string>? permissions, Guid actingManager, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(Guid employeeId, string permission, CancellationToken cancellationToken);
}
