using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Attendance;
using OptiCore.Domain.Employees;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords", table =>
        {
            table.HasCheckConstraint("CK_AttendanceRecords_Boundary", "\"AutomaticCheckoutDueAtUtc\" > \"CheckInAtUtc\"");
            table.HasCheckConstraint("CK_AttendanceRecords_Checkout", "(\"CheckOutAtUtc\" IS NULL AND \"CheckoutProcessedAtUtc\" IS NULL AND NOT \"WasCheckoutAutomatic\") OR (\"CheckOutAtUtc\" IS NOT NULL AND \"CheckoutProcessedAtUtc\" IS NOT NULL AND \"CheckOutAtUtc\" >= \"CheckInAtUtc\" AND \"CheckoutProcessedAtUtc\" >= \"CheckOutAtUtc\" AND ((\"WasCheckoutAutomatic\" AND \"CheckOutAtUtc\" = \"AutomaticCheckoutDueAtUtc\") OR (NOT \"WasCheckoutAutomatic\" AND \"UpdatedByEmployeeId\" IS NOT NULL AND \"CheckOutAtUtc\" <= \"AutomaticCheckoutDueAtUtc\")))");
        });
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();
        builder.HasOne<Employee>().WithMany().HasForeignKey(row => row.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.EmployeeId).IsUnique().HasFilter("\"CheckOutAtUtc\" IS NULL").HasDatabaseName("IX_AttendanceRecords_EmployeeId_Open");
        builder.HasIndex(row => row.AutomaticCheckoutDueAtUtc).HasFilter("\"CheckOutAtUtc\" IS NULL");
    }
}
