using OptiCore.Domain.Products;

namespace OptiCore.Application.Products;

public interface ICatalogRepository
{
    Task<Product?> GetProductAsync(int number, bool forUpdate, CancellationToken ct);
    Task<Brand?> GetBrandAsync(Guid id, bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<Brand>> ListBrandsAsync(bool includeInactive, CancellationToken ct);
    Task<(IReadOnlyList<ProductRecord> Items, int Total)> SearchAsync(ProductSearch search, CancellationToken ct);
    Task<bool> BarcodeExistsAsync(string barcode, Guid? exceptId, CancellationToken ct);
    Task<bool> BrandNameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken ct);
    Task AddProductAsync(Product product, CancellationToken ct);
    Task AddBrandAsync(Brand brand, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
