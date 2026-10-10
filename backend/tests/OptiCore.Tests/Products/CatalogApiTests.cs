using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OptiCore.Api.Endpoints.Auth;
using OptiCore.Api.Endpoints.Products;
using OptiCore.Api.Errors;
using OptiCore.Api.Security;
using OptiCore.Application;
using OptiCore.Application.Employees;
using OptiCore.Application.Permissions;
using OptiCore.Application.Products;
using OptiCore.Infrastructure.Security;
using OptiCore.Tests.Employees;
using OptiCore.Tests.Permissions;

namespace OptiCore.Tests.Products;

public sealed class CatalogApiTests
{
    [Fact]
    public void ProductContractsDoNotExposeVat()
    {
        Assert.Null(typeof(SaveProductRequest).GetProperty("VatRate"));
        Assert.Null(typeof(ProductDto).GetProperty("VatRate"));
    }

    [Fact]
    public async Task HigherPromoCreateAndUpdateReturnBadRequestWithoutSaving()
    {
        await using var host = await Host.Start(); await host.Login();
        var invalid = new { category = "Frames", regularSalePrice = 100, promoPrice = 100.01m };
        var response = await host.Client.PostAsJsonAsync("/api/products", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Promo price cannot exceed regular price.", await response.Content.ReadAsStringAsync());
        Assert.Empty(host.Catalog.Products);
        response = await host.Client.PostAsJsonAsync("/api/products", new { category = "Frames", regularSalePrice = 100 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("vatRate", await response.Content.ReadAsStringAsync());
        response = await host.Client.PutAsJsonAsync("/api/products/1", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Promo price cannot exceed regular price.", await response.Content.ReadAsStringAsync());
        Assert.Null(host.Catalog.Products.Single().PromoPrice);
        Assert.Null(host.Catalog.Products.Single().UpdatedAtUtc);
    }

    [Theory]
    [InlineData("GET", "/api/products")][InlineData("GET", "/api/products/1")]
    [InlineData("POST", "/api/products")][InlineData("PUT", "/api/products/1")]
    [InlineData("PATCH", "/api/products/1/deactivate")][InlineData("PATCH", "/api/products/1/activate")]
    [InlineData("GET", "/api/brands")][InlineData("GET", "/api/brands/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/brands")][InlineData("PUT", "/api/brands/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/brands/00000000-0000-0000-0000-000000000001/deactivate")]
    [InlineData("PATCH", "/api/brands/00000000-0000-0000-0000-000000000001/activate")]
    public async Task EveryEndpointRequiresEmployee(string method, string path)
    {
        await using var host = await Host.Start();
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task OrdinaryEmployeeManagesCatalogAndCannotForgeAuditIdentityOrNumber()
    {
        await using var host = await Host.Start(); await host.Login();
        var forged = Guid.NewGuid();
        host.Client.DefaultRequestHeaders.Add("X-Employee-Id", forged.ToString());
        var brandResponse = await host.Client.PostAsJsonAsync("/api/brands", new { name = "Test", createdByEmployeeId = forged });
        Assert.Equal(HttpStatusCode.Created, brandResponse.StatusCode);
        var b = (await brandResponse.Content.ReadFromJsonAsync<BrandDto>())!;
        Assert.Equal(host.Actor, b.CreatedByEmployeeId);
        var input = new { category = "Frames", regularSalePrice = 100, barcode = "001A", brandId = b.Id,
            productNumber = 999, createdByEmployeeId = forged, updatedByEmployeeId = forged };
        var response = await host.Client.PostAsJsonAsync("/api/products", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var p = (await response.Content.ReadFromJsonAsync<ProductDto>())!;
        Assert.Equal(host.Actor, p.CreatedByEmployeeId); Assert.NotEqual(999, p.ProductNumber);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(response.Headers.Location)).StatusCode);
        var path = $"/api/products/{p.ProductNumber}";
        var updated = await host.Client.PutAsJsonAsync(path, input);
        Assert.Equal(host.Actor, (await updated.Content.ReadFromJsonAsync<ProductDto>())!.UpdatedByEmployeeId);
        foreach (var action in new[] { "deactivate", "activate" })
        {
            var result = await host.Client.PatchAsync(path + "/" + action, null);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var dto = (await result.Content.ReadFromJsonAsync<ProductDto>())!;
            Assert.Equal(host.Actor, dto.UpdatedByEmployeeId); Assert.Equal(action == "activate", dto.IsActive);
            var brandResult = await host.Client.PatchAsync($"/api/brands/{b.Id}/{action}", null);
            Assert.Equal(host.Actor, (await brandResult.Content.ReadFromJsonAsync<BrandDto>())!.UpdatedByEmployeeId);
        }
        var renamed = await host.Client.PutAsJsonAsync($"/api/brands/{b.Id}", new { name = "Renamed", updatedByEmployeeId = forged });
        Assert.Equal(host.Actor, (await renamed.Content.ReadFromJsonAsync<BrandDto>())!.UpdatedByEmployeeId);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Client.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Client.DeleteAsync($"/api/brands/{b.Id}")).StatusCode);
        Assert.Single(host.Catalog.Products); Assert.Single(host.Catalog.Brands);
    }

    [Fact]
    public async Task ConflictsAndInvalidBodiesAreSafe()
    {
        await using var host = await Host.Start(); await host.Login();
        await host.Client.PostAsJsonAsync("/api/brands", new { name = "Ray-Ban" });
        var duplicate = await host.Client.PostAsJsonAsync("/api/brands", new { name = "ray-ban" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("Brand name already exists.", await duplicate.Content.ReadAsStringAsync());
        var input = new { category = "Other", regularSalePrice = 0, barcode = "001" };
        await host.Client.PostAsJsonAsync("/api/products", input);
        duplicate = await host.Client.PostAsJsonAsync("/api/products", input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("Product barcode already exists.", await duplicate.Content.ReadAsStringAsync());
        foreach (var invalid in new object[] { new { category = "Other" }, new { regularSalePrice = 0 },
            new { category = "0", regularSalePrice = 0 }, new { category = "Other", regularSalePrice = -1 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/products", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/products?status=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/products/999")).StatusCode);
        Assert.Single(host.Catalog.Products);
    }

    [Fact]
    public async Task ListSearchAndFiltersBindThroughApi()
    {
        await using var host = await Host.Start(); await host.Login();
        await host.Client.PostAsJsonAsync("/api/products", new { category = "Frames", regularSalePrice = 0, barcode = "001" });
        await host.Client.PostAsJsonAsync("/api/products", new { category = "Cases", regularSalePrice = 0 });
        await host.Client.PatchAsync("/api/products/1/deactivate", null);
        foreach (var query in new[] { "q=001&status=all", "q=1&status=inactive", "category=Frames&status=all" })
            Assert.Equal(1, (await host.Client.GetFromJsonAsync<ProductPage>("/api/products?" + query))!.Total);
        Assert.Equal(1, (await host.Client.GetFromJsonAsync<ProductPage>("/api/products"))!.Total);
        Assert.Equal(2, (await host.Client.GetFromJsonAsync<ProductPage>("/api/products?status=all"))!.Total);
    }

    private sealed class Host(WebApplication app, FakeCatalogRepository catalog, Guid actor) : IAsyncDisposable
    {
        public FakeCatalogRepository Catalog => catalog;
        public Guid Actor => actor;
        public HttpClient Client { get; } = new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() }) { BaseAddress = new Uri(app.Urls.Single()) };
        public async Task Login() => Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/auth/login", new { username = "employee", password = "Test-password-123" })).StatusCode);
        public static async Task<Host> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddApplication();
            var employees = new FakeEmployeeRepository();
            var employee = employees.Seed("employee", false);
            var catalog = new FakeCatalogRepository();
            builder.Services.AddSingleton<IEmployeeRepository>(employees);
            builder.Services.AddSingleton<IEmployeePermissionRepository>(new FakeEmployeePermissionRepository(employees));
            builder.Services.AddSingleton<IPasswordHasher, EmployeePasswordHasher>();
            builder.Services.AddSingleton<ICatalogRepository>(catalog);
            builder.Services.AddEmployeeAuthentication(true);
            builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            var app = builder.Build();
            app.UseExceptionHandler(); app.UseMiddleware<SameOriginRequests>(); app.UseAuthentication(); app.UseAuthorization();
            app.MapAuthEndpoints(); app.MapCatalogEndpoints();
            await app.StartAsync();
            return new(app, catalog, employee.Id);
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
