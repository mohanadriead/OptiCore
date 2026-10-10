using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Npgsql;
using OptiCore.Application.Products;
using OptiCore.Domain.Products;
using OptiCore.Infrastructure.Persistence;
using OptiCore.Infrastructure.Persistence.Migrations;
using OptiCore.Infrastructure.Persistence.Repositories;

namespace OptiCore.Tests.Products;

public sealed class CatalogPersistenceTests
{
    [Fact]
    public void FollowupMigrationOnlyRemovesProductVatSchema()
    {
        var migration = new RemoveProductVatRate();
        Assert.Equal(2, migration.UpOperations.Count);
        var constraint = Assert.IsType<DropCheckConstraintOperation>(migration.UpOperations[0]);
        Assert.Equal("Products", constraint.Table);
        Assert.Equal("CK_Products_VatRate", constraint.Name);
        var column = Assert.IsType<DropColumnOperation>(migration.UpOperations[1]);
        Assert.Equal("Products", column.Table);
        Assert.Equal("VatRate", column.Name);
        Assert.Equal(2, migration.DownOperations.Count);
        Assert.Equal("VatRate", Assert.IsType<AddColumnOperation>(migration.DownOperations[0]).Name);
        Assert.Equal("CK_Products_VatRate", Assert.IsType<AddCheckConstraintOperation>(migration.DownOperations[1]).Name);
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Product))!;
        Assert.Null(entity.FindProperty("VatRate"));
        Assert.DoesNotContain(entity.GetCheckConstraints(), c => c.Name == "CK_Products_VatRate");
        foreach (var name in new[] { "RegularSalePrice", "PromoPrice" })
        {
            Assert.Equal(18, entity.FindProperty(name)!.GetPrecision());
            Assert.Equal(2, entity.FindProperty(name)!.GetScale());
        }
    }

    [Theory]
    [InlineData("IX_Products_Barcode", typeof(DuplicateBarcodeException))]
    [InlineData("IX_Brands_NormalizedName", typeof(DuplicateBrandNameException))]
    public async Task DatabaseUniqueViolationIsTranslatedEvenAfterSuccessfulPrecheck(string constraint, Type expected)
    {
        var options = new DbContextOptionsBuilder<OptiCoreDbContext>().UseNpgsql()
            .AddInterceptors(new UniqueFailure(constraint)).Options;
        await using var db = new OptiCoreDbContext(options);
        var exception = await Record.ExceptionAsync(() => new CatalogRepository(db).SaveChangesAsync(default));
        Assert.IsType(expected, exception);
        Assert.DoesNotContain("private database detail", exception!.Message);
    }

    private sealed class UniqueFailure(string constraint) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("private database detail", new PostgresException("private database detail", "ERROR", "ERROR",
                PostgresErrorCodes.UniqueViolation, constraintName: constraint));
    }

    [Fact]
    public void MigrationOnlyAddsCatalogTablesWithIdentityUniqueConstraintsAndRestrictedBrandForeignKey()
    {
        var migration = new AddProductsAndBrands();
        Assert.All(migration.UpOperations, op => Assert.True(op is CreateTableOperation or CreateIndexOperation));
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[] { "Brands", "Products", "ProductAttributes" }, tables.Select(t => t.Name));
        var products = tables[1];
        Assert.Equal(NpgsqlValueGenerationStrategy.IdentityAlwaysColumn,
            products.Columns.Single(c => c.Name == "ProductNumber")["Npgsql:ValueGenerationStrategy"]);
        Assert.Equal(Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict, Assert.Single(products.ForeignKeys).OnDelete);
        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToArray();
        Assert.True(indexes.Single(i => i.Name == "IX_Products_ProductNumber").IsUnique);
        var barcode = indexes.Single(i => i.Name == "IX_Products_Barcode");
        Assert.True(barcode.IsUnique); Assert.Equal("\"Barcode\" IS NOT NULL", barcode.Filter);
        Assert.True(indexes.Single(i => i.Name == "IX_Brands_NormalizedName").IsUnique);
        Assert.Equal(new[] { "ProductId", "Key" }, tables[2].PrimaryKey!.Columns);
        Assert.Equal("numeric(18,2)", products.Columns.Single(c => c.Name == "RegularSalePrice").ColumnType);
        Assert.Equal(6, products.CheckConstraints.Count);
        Assert.DoesNotContain(products.Columns, c => new[] { "CostPrice", "Quantity", "SupplierId", "ExpiresAt" }.Contains(c.Name));
    }

    [Theory]
    [InlineData("Frames")][InlineData("Sunglasses")][InlineData("Lenses")][InlineData("ContactLenses")]
    [InlineData("Accessories")][InlineData("CleaningProducts")][InlineData("Cases")][InlineData("Other")]
    public void CategoryHasStableStringStorageRoundtrip(string category)
    {
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var entity = db.Model.FindEntityType(typeof(Product))!;
        var converter = entity.FindProperty("Category")!.GetTypeMapping().Converter!;
        var value = Enum.Parse<ProductCategory>(category);
        Assert.Equal(category, converter.ConvertToProvider(value));
        Assert.Equal(value, converter.ConvertFromProvider(category));
    }

    [Fact]
    public void SearchTranslatesAllCriteriaAndLiteralWildcardsWithoutConnecting()
    {
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var repository = new CatalogRepository(db);
        var sql = repository.BuildSearchQuery(new("0012%_", ProductCategory.Frames, Guid.NewGuid(), false)).ToQueryString();
        foreach (var field in new[] { "ProductNumber", "Barcode", "Model", "Color", "Brands", "Name", "Category", "BrandId", "IsActive", "ILIKE", "ORDER BY" })
            Assert.Contains(field, sql);
        Assert.Contains("\\%\\_", sql);
        Assert.Contains("12", repository.BuildSearchQuery(new("12")).ToQueryString());
    }

    [Fact]
    public void AttributeReplacementTracksUpdatesAndOrphanRemovalWithoutDuplicateKeys()
    {
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var p = new Product(ProductCategory.Frames, 1, Guid.NewGuid(), attributes: [new("A", "old"), new("B", "remove")]);
        typeof(Product).GetProperty(nameof(Product.ProductNumber))!.SetValue(p, 1);
        db.Attach(p);
        p.Update(p.Category, 2, Guid.NewGuid(), null, null, null, null, null, null, null, null, [new("A", "new"), new("C", "added")]);
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries<ProductAttribute>().ToArray();
        Assert.Equal(EntityState.Modified, entries.Single(e => e.Entity.Key == "A").State);
        Assert.Equal(EntityState.Deleted, entries.Single(e => e.Entity.Key == "B").State);
        Assert.Equal(EntityState.Added, entries.Single(e => e.Entity.Key == "C").State);
        var model = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Product))!;
        Assert.False(model.FindProperty("CreatedByEmployeeId")!.IsNullable);
    }
}
