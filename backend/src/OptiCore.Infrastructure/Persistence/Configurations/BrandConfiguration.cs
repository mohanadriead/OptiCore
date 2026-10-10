using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Products;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.ToTable("Brands", t => t.HasCheckConstraint("CK_Brands_Name", "length(btrim(\"Name\")) > 0 AND length(btrim(\"NormalizedName\")) > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.NormalizedName).IsUnique();
        b.Property(x => x.CreatedByEmployeeId).IsRequired();
        b.HasIndex(x => x.IsActive);
    }
}
