using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Attendance;
using OptiCore.Domain.Employees;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public sealed class AttendanceCorrectionConfiguration : IEntityTypeConfiguration<AttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrection> builder)
    {
        builder.ToTable("AttendanceCorrections", table =>
        {
            table.HasCheckConstraint("CK_AttendanceCorrections_Reason", "length(btrim(\"Reason\")) > 0");
            table.HasCheckConstraint("CK_AttendanceCorrections_Times", "(\"PreviousCheckOutAtUtc\" IS NULL OR \"PreviousCheckOutAtUtc\" >= \"PreviousCheckInAtUtc\") AND (\"NewCheckOutAtUtc\" IS NULL OR \"NewCheckOutAtUtc\" >= \"NewCheckInAtUtc\")");
        });
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();
        builder.Property(row => row.Reason).IsRequired().HasMaxLength(2000);
        builder.HasOne<AttendanceRecord>().WithMany().HasForeignKey(row => row.AttendanceRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(row => row.CorrectedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AttendanceRecordId, row.CorrectedAtUtc });
    }
}
