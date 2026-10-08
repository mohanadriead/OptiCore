using OptiCore.Application.Employees;
using OptiCore.Domain.Employees;
using OptiCore.Domain.Permissions;

namespace OptiCore.Application.Permissions;

public sealed class PermissionService(IEmployeeRepository employees, IEmployeePermissionRepository assignments) : IPermissionService
{
    private static readonly IReadOnlyList<PermissionDto> Catalog = Array.AsReadOnly(new[]
    {
        new PermissionDto(PermissionCatalog.ReceiveStock, "קבלת מלאי מספק"),
        new PermissionDto(PermissionCatalog.GiveDiscount, "מתן הנחות"),
        new PermissionDto(PermissionCatalog.ViewDailySales, "צפייה במכירות יומיות"),
        new PermissionDto(PermissionCatalog.ViewProfit, "צפייה ברווחים"),
        new PermissionDto(PermissionCatalog.ViewSupplierDetails, "צפייה בפרטי ספקים"),
        new PermissionDto(PermissionCatalog.ViewReports, "צפייה בדוחות"),
        new PermissionDto(PermissionCatalog.ExportData, "ייצוא נתונים"),
        new PermissionDto(PermissionCatalog.ViewAuditLogs, "צפייה ביומן ביקורת")
    });

    public async Task<IReadOnlyList<PermissionDto>> GetCatalogAsync(Guid actingManager, CancellationToken cancellationToken)
    {
        await RequireManagerAsync(actingManager, cancellationToken);
        return Catalog;
    }

    public async Task<EmployeePermissionsDto> GetAssignedPermissionsAsync(int employeeNumber, Guid actingManager, CancellationToken cancellationToken)
    {
        await RequireManagerAsync(actingManager, cancellationToken);
        var target = await FindAsync(employeeNumber, cancellationToken);
        var assigned = await assignments.GetAssignedAsync(target.Id, cancellationToken);
        return Describe(target, assigned);
    }

    public Task<EmployeePermissionsDto> SetAssignedPermissionsAsync(int employeeNumber, IReadOnlyList<string>? permissions,
        Guid actingManager, CancellationToken cancellationToken) => employees.ExecuteExclusiveAsync(async () =>
    {
        // Re-read the actor under the same lock used by deactivation and role changes.
        await RequireManagerAsync(actingManager, cancellationToken);
        var target = await FindAsync(employeeNumber, cancellationToken);
        if (permissions is null) throw new ArgumentException("Permissions are required.");
        foreach (var code in permissions) PermissionCatalog.Validate(code);
        var assigned = PermissionCatalog.Codes.Where(code => permissions.Contains(code, StringComparer.Ordinal)).ToArray();
        await assignments.ReplaceAsync(target.Id, assigned, cancellationToken);
        return Describe(target, assigned);
    }, cancellationToken);

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await RequireActiveAsync(employeeId, cancellationToken);
        return employee.IsManager ? PermissionCatalog.Codes : await assignments.GetAssignedAsync(employeeId, cancellationToken);
    }

    public async Task<bool> HasPermissionAsync(Guid employeeId, string permission, CancellationToken cancellationToken)
    {
        if (!PermissionCatalog.IsKnown(permission)) return false;
        var employee = await employees.GetByIdAsync(employeeId, false, cancellationToken);
        if (employee is null || !employee.IsActive) return false;
        return employee.IsManager || (await assignments.GetAssignedAsync(employeeId, cancellationToken)).Contains(permission, StringComparer.Ordinal);
    }

    private static EmployeePermissionsDto Describe(Employee employee, IReadOnlyList<string> assigned) =>
        new(employee.EmployeeNumber, employee.IsManager, assigned, employee.IsManager ? PermissionCatalog.Codes : assigned);

    private async Task<Employee> RequireActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(id, false, cancellationToken) ?? throw new InvalidCredentialsException();
        if (!employee.IsActive) throw new InactiveEmployeeException();
        return employee;
    }

    private async Task RequireManagerAsync(Guid actor, CancellationToken cancellationToken)
    {
        if (!(await RequireActiveAsync(actor, cancellationToken)).IsManager) throw new ManagerRequiredException();
    }

    private async Task<Employee> FindAsync(int number, CancellationToken cancellationToken) =>
        await employees.GetByNumberAsync(number, false, cancellationToken) ?? throw new EmployeeNotFoundException();
}
