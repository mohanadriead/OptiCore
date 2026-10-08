namespace OptiCore.Domain.Permissions;

public sealed class EmployeePermission
{
    public Guid EmployeeId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;

    private EmployeePermission() { }

    public EmployeePermission(Guid employeeId, string permissionCode)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.");
        PermissionCatalog.Validate(permissionCode);
        EmployeeId = employeeId;
        PermissionCode = permissionCode;
    }
}
