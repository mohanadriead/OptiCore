using Microsoft.EntityFrameworkCore;
using Npgsql;
using OptiCore.Application.Products;
using OptiCore.Domain.Products;

namespace OptiCore.Infrastructure.Persistence.Repositories;

public sealed class CatalogRepository(OptiCoreDbContext db) : ICatalogRepository
{
    public Task<Product?> GetProductAsync(int number, bool forUpdate, CancellationToken ct) =>
        (forUpdate ? db.Products.AsTracking() : db.Products.AsNoTracking()).Include(x => x.Attributes)
        .SingleOrDefaultAsync(x => x.ProductNumber == number, ct);
    public Task<Brand?> GetBrandAsync(Guid id, bool forUpdate, CancellationToken ct) =>
        (forUpdate ? db.Brands.AsTracking() : db.Brands.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Brand>> ListBrandsAsync(bool includeInactive, CancellationToken ct) =>
        await db.Brands.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<bool> BarcodeExistsAsync(string barcode, Guid? exceptId, CancellationToken ct) =>
        db.Products.AnyAsync(x => x.Barcode == barcode && (!exceptId.HasValue || x.Id != exceptId), ct);
    public Task<bool> BrandNameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken ct) =>
        db.Brands.AnyAsync(x => x.NormalizedName == normalizedName && (!exceptId.HasValue || x.Id != exceptId), ct);
    public async Task AddProductAsync(Product product, CancellationToken ct) => await db.Products.AddAsync(product, ct);
    public async Task AddBrandAsync(Brand brand, CancellationToken ct) => await db.Brands.AddAsync(brand, ct);

    // Exposed as a query for offline SQL translation tests, without a database connection.
    public IQueryable<Product> BuildSearchQuery(ProductSearch search)
    {
        var products = db.Products.AsNoTracking();
        if (search.IsActive.HasValue) products = products.Where(x => x.IsActive == search.IsActive);
        if (search.Category.HasValue) products = products.Where(x => x.Category == search.Category);
        if (search.BrandId.HasValue) products = products.Where(x => x.BrandId == search.BrandId);
        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var query = search.Query.Trim();
            var hasNumber = int.TryParse(query, out var number);
            var pattern = "%" + query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            products = products.Where(x => (hasNumber && x.ProductNumber == number) ||
                (x.Barcode != null && EF.Functions.ILike(x.Barcode, pattern, "\\")) ||
                (x.Model != null && EF.Functions.ILike(x.Model, pattern, "\\")) ||
                (x.Color != null && EF.Functions.ILike(x.Color, pattern, "\\")) ||
                db.Brands.Any(b => b.Id == x.BrandId && EF.Functions.ILike(b.Name, pattern, "\\")));
            return products.OrderByDescending(x => (hasNumber && x.ProductNumber == number) || x.Barcode == query)
                .ThenBy(x => x.ProductNumber);
        }
        return products.OrderBy(x => x.ProductNumber);
    }

    public async Task<(IReadOnlyList<ProductRecord> Items, int Total)> SearchAsync(ProductSearch search, CancellationToken ct)
    {
        var query = BuildSearchQuery(search);
        var total = await query.CountAsync(ct);
        var products = await query.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .Include(x => x.Attributes).ToListAsync(ct);
        var ids = products.Where(x => x.BrandId.HasValue).Select(x => x.BrandId!.Value).Distinct().ToArray();
        var brands = await db.Brands.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return (products.Select(x => new ProductRecord(x, x.BrandId.HasValue ? brands.GetValueOrDefault(x.BrandId.Value) : null)).ToArray(), total);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Products_Barcode" })
        { throw new DuplicateBarcodeException(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Brands_NormalizedName" })
        { throw new DuplicateBrandNameException(); }
    }
}
