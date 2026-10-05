using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Customers;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(customer => customer.CustomerNumber).UseIdentityAlwaysColumn();
        builder.HasIndex(customer => customer.CustomerNumber).IsUnique();

        builder.Property(customer => customer.NationalId).HasMaxLength(9).IsRequired();
        builder.HasIndex(customer => customer.NationalId).IsUnique();

        builder.Property(customer => customer.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(customer => customer.LastName).HasMaxLength(100).IsRequired();
        builder.Property(customer => customer.DateOfBirth).HasColumnType("date").IsRequired();
        builder.Property(customer => customer.MobilePhone).HasMaxLength(30).IsRequired();
        builder.Property(customer => customer.HomePhone).HasMaxLength(30).IsRequired(false);
        builder.Property(customer => customer.Email).HasMaxLength(254).IsRequired(false);
        builder.Property(customer => customer.City).HasMaxLength(100).IsRequired();
        builder.Property(customer => customer.Street).HasMaxLength(200).IsRequired(false);
        builder.Property(customer => customer.Gender).HasMaxLength(50).IsRequired();
        builder.Property(customer => customer.Notes).HasMaxLength(4000).IsRequired(false);
        builder.Property(customer => customer.WhatsAppConsent).IsRequired();
        builder.Property(customer => customer.IsActive).IsRequired();

        builder.Property(customer => customer.CreatedAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(customer => customer.CreatedByEmployeeId)
            .HasColumnType("uuid").IsRequired(false);
        builder.Property(customer => customer.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(customer => customer.UpdatedByEmployeeId)
            .HasColumnType("uuid").IsRequired(false);
    }
}
