using Microsoft.EntityFrameworkCore;

using OptiCore.Domain.Customers;

namespace OptiCore.Infrastructure.Persistence;

public class OptiCoreDbContext : DbContext
{
    public DbSet<OptiCore.Domain.Attendance.AttendanceRecord> AttendanceRecords => Set<OptiCore.Domain.Attendance.AttendanceRecord>();
    public DbSet<OptiCore.Domain.Attendance.AttendanceCorrection> AttendanceCorrections => Set<OptiCore.Domain.Attendance.AttendanceCorrection>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<OptiCore.Domain.Employees.Employee> Employees => Set<OptiCore.Domain.Employees.Employee>();
    public DbSet<OptiCore.Domain.Permissions.EmployeePermission> EmployeePermissions => Set<OptiCore.Domain.Permissions.EmployeePermission>();

    public OptiCoreDbContext(DbContextOptions<OptiCoreDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OptiCoreDbContext).Assembly);
    }
}
