namespace OptiCore.Application.Products;

public interface ICatalogService
{
    Task<ProductPage> SearchAsync(string? q, string? category, Guid? brandId, bool? isActive, int page, int pageSize, CancellationToken ct);
    Task<ProductDto> GetProductAsync(int number, CancellationToken ct);
    Task<ProductDto> CreateProductAsync(SaveProductRequest request, Guid actor, CancellationToken ct);
    Task<ProductDto> UpdateProductAsync(int number, SaveProductRequest request, Guid actor, CancellationToken ct);
    Task<ProductDto> SetProductActiveAsync(int number, bool active, Guid actor, CancellationToken ct);
    Task<IReadOnlyList<BrandDto>> ListBrandsAsync(bool includeInactive, CancellationToken ct);
    Task<BrandDto> GetBrandAsync(Guid id, CancellationToken ct);
    Task<BrandDto> CreateBrandAsync(SaveBrandRequest request, Guid actor, CancellationToken ct);
    Task<BrandDto> UpdateBrandAsync(Guid id, SaveBrandRequest request, Guid actor, CancellationToken ct);
    Task<BrandDto> SetBrandActiveAsync(Guid id, bool active, Guid actor, CancellationToken ct);
}
