using Microsoft.EntityFrameworkCore;
using OptiCore.Application.Permissions;
using OptiCore.Domain.Permissions;

namespace OptiCore.Infrastructure.Persistence.Repositories;

public sealed class EmployeePermissionRepository(OptiCoreDbContext db) : IEmployeePermissionRepository
{
    public async Task<IReadOnlyList<string>> GetAssignedAsync(Guid employeeId, CancellationToken cancellationToken) =>
        await db.EmployeePermissions.AsNoTracking().Where(permission => permission.EmployeeId == employeeId)
            .OrderBy(permission => permission.PermissionCode).Select(permission => permission.PermissionCode).ToArrayAsync(cancellationToken);

    public async Task ReplaceAsync(Guid employeeId, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Permission replacement requires an employee transaction.");
        // Validate all input before changing tracked rows; SaveChanges and the outer
        // transaction make deletion/insertion atomic. Keep unchanged keys tracked once.
        var desired = permissions.Select(code => new EmployeePermission(employeeId, code)).DistinctBy(row => row.PermissionCode).ToArray();
        var existing = await db.EmployeePermissions.Where(row => row.EmployeeId == employeeId).ToListAsync(cancellationToken);
        db.EmployeePermissions.RemoveRange(existing.Where(row => !desired.Any(next => next.PermissionCode == row.PermissionCode)));
        db.EmployeePermissions.AddRange(desired.Where(row => !existing.Any(previous => previous.PermissionCode == row.PermissionCode)));
        await db.SaveChangesAsync(cancellationToken);
    }
}
