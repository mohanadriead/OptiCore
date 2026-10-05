using OptiCore.Infrastructure;
using OptiCore.Infrastructure.Persistence;
using OptiCore.Application;
using OptiCore.Api.Endpoints.Customers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomerExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapCustomerEndpoints();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok"
    });
});

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

app.Run();
