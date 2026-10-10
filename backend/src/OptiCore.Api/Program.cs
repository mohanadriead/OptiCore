using OptiCore.Api.Errors;
using OptiCore.Api.Hosting;
using OptiCore.Api.Endpoints.Products;
using OptiCore.Infrastructure;
using OptiCore.Infrastructure.Persistence;
using OptiCore.Application;
using OptiCore.Api.Endpoints.Customers;
using OptiCore.Api.Endpoints.Auth;
using OptiCore.Api.Endpoints.Employees;
using OptiCore.Api.Endpoints.Permissions;
using OptiCore.Api.Security;
using OptiCore.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureWebHosting();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEmployeeAuthentication(builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseExceptionHandler();
app.UseFrontendFiles();
app.UseMiddleware<SameOriginRequests>();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapEmployeeEndpoints();
app.MapCustomerEndpoints();
app.MapPermissionEndpoints();
app.MapCatalogEndpoints();

app.MapLiveness();
app.MapFrontendRoutes();

app.MapGet("/health/database", async (OptiCoreDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    if (!canConnect)
    {
        return Results.Problem("Database connection failed.");
    }

    return Results.Ok(new
    {
        status = "ok",
        database = "connected"
    });
});

// Explicit startup initialization; EF tooling stops after host construction and does not seed accounts.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<BootstrapManager>().InitializeAsync(CancellationToken.None);
}

app.Run();
