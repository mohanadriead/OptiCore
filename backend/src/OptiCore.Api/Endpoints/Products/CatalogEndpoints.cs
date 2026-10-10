using OptiCore.Api.Security;
using OptiCore.Application.Products;

namespace OptiCore.Api.Endpoints.Products;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").RequireAuthorization("Employee");
        products.MapGet("", async (string? q, string? category, Guid? brandId, string? status,
            int? page, int? pageSize, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(q, category, brandId, status switch
            { null or "active" => true, "inactive" => false, "all" => (bool?)null,
                _ => throw new ArgumentException("Invalid catalog status.") }, page ?? 1, pageSize ?? 50, ct)));
        products.MapGet("/{productNumber:int}", async (int productNumber, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.GetProductAsync(productNumber, ct)));
        products.MapPost("", async (SaveProductRequest request, HttpContext context, ICatalogService service, CancellationToken ct) =>
        {
            var product = await service.CreateProductAsync(request, CurrentEmployee.Read(context), ct);
            return Results.Created($"/api/products/{product.ProductNumber}", product);
        });
        products.MapPut("/{productNumber:int}", async (int productNumber, SaveProductRequest request,
            HttpContext context, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateProductAsync(productNumber, request, CurrentEmployee.Read(context), ct)));
        products.MapPatch("/{productNumber:int}/activate", async (int productNumber, HttpContext context, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.SetProductActiveAsync(productNumber, true, CurrentEmployee.Read(context), ct)));
        products.MapPatch("/{productNumber:int}/deactivate", async (int productNumber, HttpContext context, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.SetProductActiveAsync(productNumber, false, CurrentEmployee.Read(context), ct)));

        var brands = app.MapGroup("/api/brands").RequireAuthorization("Employee");
        brands.MapGet("", async (bool? includeInactive, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.ListBrandsAsync(includeInactive ?? false, ct)));
        brands.MapGet("/{id:guid}", async (Guid id, ICatalogService service, CancellationToken ct) => Results.Ok(await service.GetBrandAsync(id, ct)));
        brands.MapPost("", async (SaveBrandRequest request, HttpContext context, ICatalogService service, CancellationToken ct) =>
        {
            var brand = await service.CreateBrandAsync(request, CurrentEmployee.Read(context), ct);
            return Results.Created($"/api/brands/{brand.Id}", brand);
        });
        brands.MapPut("/{id:guid}", async (Guid id, SaveBrandRequest request, HttpContext context, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateBrandAsync(id, request, CurrentEmployee.Read(context), ct)));
        brands.MapPatch("/{id:guid}/activate", async (Guid id, HttpContext context, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.SetBrandActiveAsync(id, true, CurrentEmployee.Read(context), ct)));
        brands.MapPatch("/{id:guid}/deactivate", async (Guid id, HttpContext context, ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.SetBrandActiveAsync(id, false, CurrentEmployee.Read(context), ct)));
        return app;
    }
}
