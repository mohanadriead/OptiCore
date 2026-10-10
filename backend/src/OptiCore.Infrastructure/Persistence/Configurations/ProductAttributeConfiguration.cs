using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptiCore.Domain.Products;

namespace OptiCore.Infrastructure.Persistence.Configurations;

public sealed class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> b)
    {
        b.ToTable("ProductAttributes", t => t.HasCheckConstraint("CK_ProductAttributes_Text",
            "length(btrim(\"Key\")) > 0 AND length(btrim(\"Value\")) > 0 AND \"Key\" = btrim(\"Key\") AND \"Value\" = btrim(\"Value\")"));
        b.HasKey(x => new { x.ProductId, x.Key });
        b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
    }
}
