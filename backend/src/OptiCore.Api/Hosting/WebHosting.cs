using System.Globalization;

namespace OptiCore.Api.Hosting;

public static class WebHosting
{
    public static void ConfigureWebHosting(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(PublicOrigin.Read(builder.Configuration, builder.Environment));
        var port = builder.Configuration["PORT"];
        if (port is not null)
        {
            if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535)
                throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");
            builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
        }
    }

    public static void UseFrontendFiles(this WebApplication app)
    {
        // Static files must not mask API or health endpoints, even if a file with
        // a matching name is accidentally included in wwwroot.
        app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api") &&
            !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseStaticFiles());
    }

    public static void MapFrontendRoutes(this WebApplication app)
    {
        var options = new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
        };
        // Only React route namespaces get the SPA document. Missing assets,
        // unknown API paths and unsupported HTTP methods retain normal errors.
        app.MapFallbackToFile("/", "index.html", options);
        foreach (var route in new[] { "login", "change-password", "customers", "products", "brands", "employees" })
            app.MapFallbackToFile($"/{route}/{{*path:nonfile}}", "index.html", options);
    }

    public static void MapLiveness(this IEndpointRouteBuilder app) =>
        app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
}
