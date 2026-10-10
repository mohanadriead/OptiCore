using System.Text.Json.Serialization;
using OptiCore.Domain.Products;

namespace OptiCore.Application.Products;

// String contracts keep stable English identifiers on the wire and reject unknown/numeric enum values.
public sealed record ProductAttributeRequest(string Key, string Value);
public sealed record SaveProductRequest(
    [property: JsonRequired] string Category,
    [property: JsonRequired] decimal RegularSalePrice,
    string? Barcode = null, Guid? BrandId = null, string? Model = null, string? Color = null,
    string? Size = null, string? GenderCategory = null, string? LensType = null,
    decimal? PromoPrice = null, IReadOnlyList<ProductAttributeRequest>? Attributes = null);
public sealed record SaveBrandRequest(string Name);
public sealed record BrandDto(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAtUtc,
    Guid? CreatedByEmployeeId, DateTimeOffset? UpdatedAtUtc, Guid? UpdatedByEmployeeId)
{
    public static BrandDto From(Brand brand) => new(brand.Id, brand.Name, brand.IsActive, brand.CreatedAtUtc,
        brand.CreatedByEmployeeId, brand.UpdatedAtUtc, brand.UpdatedByEmployeeId);
}
public sealed record ProductDto(Guid Id, int ProductNumber, string? Barcode, string Category,
    Guid? BrandId, string? BrandName, bool? BrandIsActive, string? Model, string? Color, string? Size,
    string? GenderCategory, string? LensType, decimal RegularSalePrice, decimal? PromoPrice,
    bool IsActive, IReadOnlyList<ProductAttributeRequest> Attributes, DateTimeOffset CreatedAtUtc,
    Guid? CreatedByEmployeeId, DateTimeOffset? UpdatedAtUtc, Guid? UpdatedByEmployeeId)
{
    public static ProductDto From(Product product, Brand? brand) => new(product.Id, product.ProductNumber,
        product.Barcode, product.Category.ToString(), product.BrandId, brand?.Name, brand?.IsActive,
        product.Model, product.Color, product.Size, product.GenderCategory?.ToString(), product.LensType,
        product.RegularSalePrice, product.PromoPrice, product.IsActive,
        product.Attributes.OrderBy(a => a.Key).Select(a => new ProductAttributeRequest(a.Key, a.Value)).ToArray(),
        product.CreatedAtUtc, product.CreatedByEmployeeId, product.UpdatedAtUtc, product.UpdatedByEmployeeId);
}
public sealed record ProductSearch(string? Query = null, ProductCategory? Category = null, Guid? BrandId = null,
    bool? IsActive = true, int Page = 1, int PageSize = 50);
public sealed record ProductPage(IReadOnlyList<ProductDto> Items, int Total, int Page, int PageSize);
public sealed record ProductRecord(Product Product, Brand? Brand);
