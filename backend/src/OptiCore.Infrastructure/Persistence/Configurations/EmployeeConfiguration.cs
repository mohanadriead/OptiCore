using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Employees;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(employee => employee.EmployeeNumber).UseIdentityAlwaysColumn();
        builder.HasIndex(employee => employee.EmployeeNumber).IsUnique();
        builder.Property(employee => employee.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.LastName).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.Username).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.NormalizedUsername).HasMaxLength(100).IsRequired();
        builder.HasIndex(employee => employee.NormalizedUsername).IsUnique();
        builder.Property(employee => employee.NationalId).HasMaxLength(9).IsRequired();
        builder.HasIndex(employee => employee.NationalId).IsUnique();
        builder.Property(employee => employee.Phone).HasMaxLength(30).IsRequired();
        builder.Property(employee => employee.PasswordHash).HasMaxLength(1024).IsRequired();
        builder.Property(employee => employee.IsActive).IsRequired();
        builder.Property(employee => employee.IsManager).IsRequired();
        builder.Property(employee => employee.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(employee => employee.CreatedByEmployeeId).HasColumnType("uuid").IsRequired(false);
        builder.Property(employee => employee.UpdatedAtUtc).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(employee => employee.UpdatedByEmployeeId).HasColumnType("uuid").IsRequired(false);
    }
}
