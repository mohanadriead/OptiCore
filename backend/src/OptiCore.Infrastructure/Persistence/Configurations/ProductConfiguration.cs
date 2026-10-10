using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Products;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Prices", "\"RegularSalePrice\" >= 0 AND (\"PromoPrice\" IS NULL OR \"PromoPrice\" >= 0)");
            t.HasCheckConstraint("CK_Products_Category", "\"Category\" IN ('Frames','Sunglasses','Lenses','ContactLenses','Accessories','CleaningProducts','Cases','Other')");
            t.HasCheckConstraint("CK_Products_GenderCategory", "\"GenderCategory\" IS NULL OR (\"Category\" IN ('Frames','Sunglasses') AND \"GenderCategory\" IN ('Men','Women','Unisex','Kids'))");
            t.HasCheckConstraint("CK_Products_LensType", "\"LensType\" IS NULL OR \"Category\" = 'Lenses'");
            t.HasCheckConstraint("CK_Products_Barcode", "\"Barcode\" IS NULL OR (length(\"Barcode\") > 0 AND \"Barcode\" = btrim(\"Barcode\"))");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ProductNumber).UseIdentityAlwaysColumn();
        b.HasIndex(x => x.ProductNumber).IsUnique();
        b.Property(x => x.Barcode).HasMaxLength(100);
        b.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.GenderCategory).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Model).HasMaxLength(200);
        b.Property(x => x.Color).HasMaxLength(100);
        b.Property(x => x.Size).HasMaxLength(100);
        b.Property(x => x.LensType).HasMaxLength(200);
        b.Property(x => x.RegularSalePrice).HasPrecision(18, 2);
        b.Property(x => x.PromoPrice).HasPrecision(18, 2);
        b.Property(x => x.CreatedByEmployeeId).IsRequired();
        b.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Category, x.IsActive });
        b.HasIndex(x => new { x.IsActive, x.ProductNumber });
        b.HasMany(x => x.Attributes).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Attributes).HasField("attributes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
