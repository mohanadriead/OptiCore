using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using OptiCore.Api.Endpoints.Auth;
using OptiCore.Api.Endpoints.Customers;
using OptiCore.Api.Endpoints.Employees;
using OptiCore.Api.Endpoints.Products;
using OptiCore.Api.Errors;
using OptiCore.Api.Hosting;
using OptiCore.Api.Security;
using OptiCore.Application;
using OptiCore.Application.Customers;
using OptiCore.Application.Employees;
using OptiCore.Application.Permissions;
using OptiCore.Application.Products;
using OptiCore.Infrastructure.Security;
using OptiCore.Tests.Employees;
using OptiCore.Tests.Permissions;
using OptiCore.Tests.Products;

namespace OptiCore.Tests.Hosting;

public sealed class WebHostingTests
{
    [Theory]
    [InlineData("/")][InlineData("/login")][InlineData("/customers")]
    [InlineData("/products/5")][InlineData("/products/5/edit")]
    [InlineData("/brands")][InlineData("/employees")][InlineData("/change-password")]
    public async Task DirectSpaRoutesServeIndex(string path)
    {
        await using var host = await Host.Start();
        var response = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("opticore-test-index", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task StaticAssetsAndLivenessWorkWithoutDatabase()
    {
        await using var host = await Host.Start();
        var asset = await host.Client.GetAsync("/assets/app.js");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("test-asset", await asset.Content.ReadAsStringAsync());
        var health = await host.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", await health.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api")][InlineData("/api/missing")][InlineData("/api/missing.js")]
    [InlineData("/API/missing")][InlineData("/health/missing")]
    [InlineData("/assets/missing.js")][InlineData("/products/missing.js")][InlineData("/unrelated")]
    public async Task MissingApiHealthAndAssetsNeverReceiveIndex(string path)
    {
        await using var host = await Host.Start();
        host.Client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        var response = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("opticore-test-index", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostToSpaIsNotAnHtmlFallback()
    {
        await using var host = await Host.Start();
        var response = await host.Client.PostAsync("/products/5", null);
        Assert.False(response.IsSuccessStatusCode);
        Assert.DoesNotContain("opticore-test-index", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiStatusesAndProductionCookieSecuritySurviveStaticHosting()
    {
        await using var host = await Host.Start();
        foreach (var path in new[] { "/api/auth/me", "/api/customers/42", "/api/products", "/api/brands", "/api/employees" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(path)).StatusCode);
        var login = await host.Login();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("__Host-OptiCore.Auth=", cookie);
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=lax", cookie); Assert.Contains("path=/", cookie);
        Assert.DoesNotContain("expires=", cookie); Assert.DoesNotContain("max-age=", cookie);
        host.Client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/products/999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/employees")).StatusCode);
        var failure = await host.Client.GetAsync("/api/test-error");
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        var error = await failure.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-test-detail", error);
        Assert.DoesNotContain("opticore-test-index", error);
        host.Employees.Employees[0].SetManagerStatus(true, host.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/employees")).StatusCode);
        host.Employees.Employees[0].Deactivate(host.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Login()).StatusCode);
    }

    [Theory]
    [InlineData("https://foreign.example", "staging.example.test", null)]
    [InlineData("http://staging.example.test", "staging.example.test", null)]
    [InlineData("https://staging.example.test", "foreign.example", null)]
    [InlineData("https://staging.example.test", "staging.example.test", "cross-site")]
    public async Task ConfiguredProxyOriginRejectsForeignRequestsAndSpoofedHeaders(string origin, string hostHeader, string? fetchSite)
    {
        await using var host = await Host.Start();
        host.Client.DefaultRequestHeaders.Remove("Origin");
        host.Client.DefaultRequestHeaders.Add("Origin", origin);
        host.Client.DefaultRequestHeaders.Host = hostHeader;
        host.Client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        host.Client.DefaultRequestHeaders.Add("X-Forwarded-Host", "staging.example.test");
        if (fetchSite is not null) host.Client.DefaultRequestHeaders.Add("Sec-Fetch-Site", fetchSite);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Login()).StatusCode);
    }

    [Fact]
    public void SessionLifetimeRemainsAbsoluteAndNonSliding()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddEmployeeAuthentication(false);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        Assert.Equal(TimeSpan.FromDays(14), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(typeof(EmployeeCookieEvents), options.EventsType);
    }

    [Theory]
    [InlineData("http://staging.example.test")][InlineData("https://staging.example.test/path")]
    [InlineData("https://user:invalid@staging.example.test")][InlineData("https://staging.example.test/?query")]
    public void InvalidPublicOriginsFailWithoutEchoingValues(string value)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.Configuration["Hosting:PublicOrigin"] = value;
        var error = Assert.Throws<InvalidOperationException>(() => builder.ConfigureWebHosting());
        Assert.DoesNotContain(value, error.Message);
    }

    [Fact]
    public void StagingRequiresOriginAndProviderPortBindsAllInterfaces()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.Configuration["Hosting:PublicOrigin"] = null;
        Assert.Throws<InvalidOperationException>(() => builder.ConfigureWebHosting());
        builder.Configuration["Hosting:PublicOrigin"] = "https://staging.example.test";
        builder.Configuration["PORT"] = "10000";
        builder.ConfigureWebHosting();
        Assert.Equal("http://0.0.0.0:10000", builder.Configuration["urls"]);
    }

    [Theory]
    [InlineData("0")][InlineData("65536")][InlineData("not-a-port")][InlineData("8080;bad")]
    public void InvalidPortsFailSafely(string port)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration["PORT"] = port;
        Assert.Throws<InvalidOperationException>(() => builder.ConfigureWebHosting());
    }

    private sealed class Host(WebApplication app, string root, FakeEmployeeRepository employees) : IAsyncDisposable
    {
        public FakeEmployeeRepository Employees => employees;
        // Simulate a TLS-terminating proxy over loopback. No real HTTP browser is
        // asked to send a Secure cookie; the test explicitly forwards its cookie.
        public HttpClient Client { get; } = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { BaseAddress = new Uri(app.Urls.Single()) };
        public Task<HttpResponseMessage> Login() => Client.PostAsJsonAsync("/api/auth/login", new { username = "employee", password = "Test-password-123" });
        public static async Task<Host> Start()
        {
            var root = Path.Combine(Path.GetTempPath(), "opticore-hosting-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "wwwroot", "assets"));
            Directory.CreateDirectory(Path.Combine(root, "wwwroot", "api"));
            Directory.CreateDirectory(Path.Combine(root, "wwwroot", "health"));
            await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "index.html"), "<!doctype html><title>opticore-test-index</title>");
            await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "assets", "app.js"), "// test-asset");
            await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "api", "missing.js"), "must-not-be-served");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging", ContentRootPath = root, WebRootPath = "wwwroot" });
            builder.Logging.ClearProviders();
            builder.Configuration["Hosting:PublicOrigin"] = "https://staging.example.test";
            builder.Configuration["PORT"] = null;
            builder.ConfigureWebHosting();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddApplication();
            var employees = new FakeEmployeeRepository(); employees.Seed("employee", false);
            builder.Services.AddSingleton<IEmployeeRepository>(employees);
            builder.Services.AddSingleton<IEmployeePermissionRepository>(new FakeEmployeePermissionRepository(employees));
            builder.Services.AddSingleton<IPasswordHasher, EmployeePasswordHasher>();
            builder.Services.AddSingleton<ICustomerRepository>(new AuthenticationApiTests.TestCustomers());
            builder.Services.AddSingleton<ICatalogRepository>(new FakeCatalogRepository());
            builder.Services.AddEmployeeAuthentication(false);
            builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            var app = builder.Build();
            app.UseExceptionHandler(); app.UseFrontendFiles(); app.UseMiddleware<SameOriginRequests>();
            app.UseAuthentication(); app.UseAuthorization();
            app.MapLiveness(); app.MapAuthEndpoints(); app.MapCustomerEndpoints(); app.MapEmployeeEndpoints(); app.MapCatalogEndpoints();
            app.MapGet("/api/test-error", (Func<IResult>)(() => throw new InvalidOperationException("private-test-detail")));
            app.MapFrontendRoutes();
            await app.StartAsync();
            var host = new Host(app, root, employees);
            host.Client.DefaultRequestHeaders.Host = "staging.example.test";
            host.Client.DefaultRequestHeaders.Add("Origin", "https://staging.example.test");
            return host;
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await app.StopAsync(); await app.DisposeAsync();
            // Only the exact per-test directory created above is removed.
            Directory.Delete(root, recursive: true);
        }
    }
}
