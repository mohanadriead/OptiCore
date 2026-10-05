using OptiCore.Infrastructure;
using OptiCore.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

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