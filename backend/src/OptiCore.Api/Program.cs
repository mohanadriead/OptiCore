using Microsoft.EntityFrameworkCore;
using OptiCore.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("OptiCoreDatabase");

builder.Services.AddDbContext<OptiCoreDbContext>(options =>
    options.UseNpgsql(connectionString));

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