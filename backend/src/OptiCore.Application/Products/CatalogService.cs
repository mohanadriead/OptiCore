using OptiCore.Domain.Products;

namespace OptiCore.Application.Products;

public sealed class CatalogService(ICatalogRepository repository) : ICatalogService
{
    public static T ParseCode<T>(string value) where T : struct, Enum =>
        Enum.GetNames<T>().Contains(value, StringComparer.Ordinal) && Enum.TryParse<T>(value, out var parsed)
            ? parsed : throw new ArgumentException("Invalid catalog category.");

    public async Task<ProductPage> SearchAsync(string? q, string? category, Guid? brandId, bool? isActive,
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100)
            throw new ArgumentException("Invalid catalog page.");
        var search = new ProductSearch(CatalogValidation.Optional(q, 200),
            string.IsNullOrEmpty(category) ? null : ParseCode<ProductCategory>(category), brandId, isActive, page, pageSize);
        var result = await repository.SearchAsync(search, ct);
        return new(result.Items.Select(r => ProductDto.From(r.Product, r.Brand)).ToArray(), result.Total, page, pageSize);
    }

    public async Task<ProductDto> GetProductAsync(int number, CancellationToken ct) =>
        await ToDtoAsync(await FindProductAsync(number, false, ct), ct);

    private static Product Candidate(SaveProductRequest r, Guid actor) => new(
        ParseCode<ProductCategory>(r.Category), r.RegularSalePrice, actor, r.Barcode, r.BrandId,
        r.Model, r.Color, r.Size, r.GenderCategory is null ? null : ParseCode<GenderCategory>(r.GenderCategory),
        r.LensType, r.PromoPrice, r.Attributes?.Select(a => a is null
            ? throw new ArgumentException("Invalid product attribute.") : new ProductAttribute(a.Key, a.Value)));

    private async Task CheckProductAsync(Product candidate, Product? existing, CancellationToken ct)
    {
        if (candidate.BrandId is Guid brandId)
        {
            var brand = await FindBrandAsync(brandId, false, ct);
            if (!brand.IsActive && existing?.BrandId != brandId) throw new InactiveBrandException();
        }
        if (candidate.Barcode is not null && await repository.BarcodeExistsAsync(candidate.Barcode, existing?.Id, ct))
            throw new DuplicateBarcodeException();
    }

    public async Task<ProductDto> CreateProductAsync(SaveProductRequest request, Guid actor, CancellationToken ct)
    {
        var product = Candidate(request, actor);
        await CheckProductAsync(product, null, ct);
        await repository.AddProductAsync(product, ct);
        await repository.SaveChangesAsync(ct);
        return await ToDtoAsync(product, ct);
    }

    public async Task<ProductDto> UpdateProductAsync(int number, SaveProductRequest request, Guid actor, CancellationToken ct)
    {
        var product = await FindProductAsync(number, true, ct);
        var next = Candidate(request, actor);
        await CheckProductAsync(next, product, ct);
        product.Update(next.Category, next.RegularSalePrice, actor, next.Barcode, next.BrandId,
            next.Model, next.Color, next.Size, next.GenderCategory, next.LensType, next.PromoPrice, next.Attributes);
        await repository.SaveChangesAsync(ct);
        return await ToDtoAsync(product, ct);
    }

    public async Task<ProductDto> SetProductActiveAsync(int number, bool active, Guid actor, CancellationToken ct)
    {
        var product = await FindProductAsync(number, true, ct);
        product.SetActive(active, actor);
        await repository.SaveChangesAsync(ct);
        return await ToDtoAsync(product, ct);
    }

    public async Task<IReadOnlyList<BrandDto>> ListBrandsAsync(bool includeInactive, CancellationToken ct) =>
        (await repository.ListBrandsAsync(includeInactive, ct)).Select(BrandDto.From).ToArray();
    public async Task<BrandDto> GetBrandAsync(Guid id, CancellationToken ct) => BrandDto.From(await FindBrandAsync(id, false, ct));

    public async Task<BrandDto> CreateBrandAsync(SaveBrandRequest request, Guid actor, CancellationToken ct)
    {
        var brand = new Brand(request.Name, actor);
        if (await repository.BrandNameExistsAsync(brand.NormalizedName, null, ct)) throw new DuplicateBrandNameException();
        await repository.AddBrandAsync(brand, ct);
        await repository.SaveChangesAsync(ct);
        return BrandDto.From(brand);
    }

    public async Task<BrandDto> UpdateBrandAsync(Guid id, SaveBrandRequest request, Guid actor, CancellationToken ct)
    {
        var brand = await FindBrandAsync(id, true, ct);
        if (await repository.BrandNameExistsAsync(Brand.NormalizeName(request.Name), id, ct)) throw new DuplicateBrandNameException();
        brand.Rename(request.Name, actor);
        await repository.SaveChangesAsync(ct);
        return BrandDto.From(brand);
    }

    public async Task<BrandDto> SetBrandActiveAsync(Guid id, bool active, Guid actor, CancellationToken ct)
    {
        var brand = await FindBrandAsync(id, true, ct);
        brand.SetActive(active, actor);
        await repository.SaveChangesAsync(ct);
        return BrandDto.From(brand);
    }

    private async Task<Product> FindProductAsync(int number, bool forUpdate, CancellationToken ct) =>
        await repository.GetProductAsync(number, forUpdate, ct) ?? throw new ProductNotFoundException();
    private async Task<Brand> FindBrandAsync(Guid id, bool forUpdate, CancellationToken ct) =>
        await repository.GetBrandAsync(id, forUpdate, ct) ?? throw new BrandNotFoundException();
    private async Task<ProductDto> ToDtoAsync(Product product, CancellationToken ct) => ProductDto.From(product,
        product.BrandId is Guid id ? await repository.GetBrandAsync(id, false, ct) : null);
}
