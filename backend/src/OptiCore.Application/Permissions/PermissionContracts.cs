namespace OptiCore.Application.Permissions;

public sealed record PermissionDto(string Code, string Label);
public sealed record EmployeePermissionsDto(int EmployeeNumber, bool IsManager,
    IReadOnlyList<string> AssignedPermissions, IReadOnlyList<string> EffectivePermissions);
public sealed record SetPermissionsRequest
{
    public required string[] Permissions { get; init; }
}
