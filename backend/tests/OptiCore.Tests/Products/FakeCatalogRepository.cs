using OptiCore.Application.Products;
using OptiCore.Domain.Products;

namespace OptiCore.Tests.Products;

internal sealed class FakeCatalogRepository : ICatalogRepository
{
    public List<Product> Products { get; } = [];
    public List<Brand> Brands { get; } = [];
    public Task<Product?> GetProductAsync(int number, bool forUpdate, CancellationToken ct) => Task.FromResult(Products.SingleOrDefault(p => p.ProductNumber == number));
    public Task<Brand?> GetBrandAsync(Guid id, bool forUpdate, CancellationToken ct) => Task.FromResult(Brands.SingleOrDefault(b => b.Id == id));
    public Task<IReadOnlyList<Brand>> ListBrandsAsync(bool includeInactive, CancellationToken ct) => Task.FromResult<IReadOnlyList<Brand>>(Brands.Where(b => includeInactive || b.IsActive).ToArray());
    public Task<bool> BarcodeExistsAsync(string barcode, Guid? exceptId, CancellationToken ct) => Task.FromResult(Products.Any(p => p.Barcode == barcode && p.Id != exceptId));
    public Task<bool> BrandNameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken ct) => Task.FromResult(Brands.Any(b => b.NormalizedName == normalizedName && b.Id != exceptId));
    public Task AddProductAsync(Product product, CancellationToken ct) { Products.Add(product); return Task.CompletedTask; }
    public Task AddBrandAsync(Brand brand, CancellationToken ct) { Brands.Add(brand); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct)
    {
        var next = Products.Select(p => p.ProductNumber).DefaultIfEmpty().Max();
        foreach (var p in Products.Where(p => p.ProductNumber == 0)) typeof(Product).GetProperty(nameof(Product.ProductNumber))!.SetValue(p, ++next);
        return Task.CompletedTask;
    }
    public Task<(IReadOnlyList<ProductRecord> Items, int Total)> SearchAsync(ProductSearch search, CancellationToken ct)
    {
        var query = Products.Where(p => (!search.IsActive.HasValue || p.IsActive == search.IsActive) &&
            (!search.Category.HasValue || p.Category == search.Category) && (!search.BrandId.HasValue || p.BrandId == search.BrandId));
        if (search.Query is string q)
            query = query.Where(p => p.ProductNumber.ToString() == q || new[] { p.Barcode, p.Model, p.Color, Brands.SingleOrDefault(b => b.Id == p.BrandId)?.Name }
                .Any(value => value?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
        var all = query.OrderBy(p => p.ProductNumber).ToArray();
        IReadOnlyList<ProductRecord> items = all.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .Select(p => new ProductRecord(p, Brands.SingleOrDefault(b => b.Id == p.BrandId))).ToArray();
        return Task.FromResult((items, all.Length));
    }
}
