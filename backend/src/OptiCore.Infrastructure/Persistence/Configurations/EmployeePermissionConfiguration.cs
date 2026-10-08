using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Employees;
using OptiCore.Domain.Permissions;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public sealed class EmployeePermissionConfiguration : IEntityTypeConfiguration<EmployeePermission>
{
    public void Configure(EntityTypeBuilder<EmployeePermission> builder)
    {
        var knownCodes = string.Join(", ", PermissionCatalog.Codes.Select(code => $"'{code}'"));
        builder.ToTable("EmployeePermissions", table => table.HasCheckConstraint(
            "CK_EmployeePermissions_PermissionCode", $"\"PermissionCode\" IN ({knownCodes})"));
        builder.HasKey(permission => new { permission.EmployeeId, permission.PermissionCode });
        builder.Property(permission => permission.EmployeeId).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(permission => permission.PermissionCode).HasMaxLength(PermissionCatalog.MaximumCodeLength).IsRequired();
        builder.HasOne<Employee>().WithMany().HasForeignKey(permission => permission.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
