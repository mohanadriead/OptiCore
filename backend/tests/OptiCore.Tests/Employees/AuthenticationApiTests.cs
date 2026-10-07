using OptiCore.Api.Errors;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OptiCore.Api.Endpoints.Auth;
using OptiCore.Api.Endpoints.Customers;
using OptiCore.Api.Endpoints.Employees;
using OptiCore.Api.Security;
using OptiCore.Application;
using OptiCore.Application.Customers;
using OptiCore.Application.Employees;
using OptiCore.Domain.Customers;
using OptiCore.Infrastructure.Security;

namespace OptiCore.Tests.Employees;

public sealed class AuthenticationApiTests
{
    [Fact]
    public void MissingOrInvalidClaimFailsSafely()
    {
        Assert.Throws<InvalidCredentialsException>(() => CurrentEmployee.Read(new System.Security.Claims.ClaimsPrincipal()));
        var identity = new System.Security.Claims.ClaimsIdentity([new(System.Security.Claims.ClaimTypes.NameIdentifier, "not-a-guid")], "Cookies");
        Assert.Throws<InvalidCredentialsException>(() => CurrentEmployee.Read(new System.Security.Claims.ClaimsPrincipal(identity)));
    }

    [Fact]
    public void ProductionCookieRequiresSecureTransport()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddEmployeeAuthentication(false);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get("Cookies");
        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal("__Host-OptiCore.Auth", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.False(options.SlidingExpiration);
    }

    [Fact]
    public async Task LoginCookieMeAndLogout()
    {
        await using var host = await ApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        using var login = await host.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());
        Assert.DoesNotContain("expires=", cookie.ToLowerInvariant());
        Assert.DoesNotContain("password", (await login.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
        using var logout = await host.Client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=", Assert.Single(logout.Headers.GetValues("Set-Cookie")).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("missing", "wrong", HttpStatusCode.Unauthorized)]
    [InlineData("manager", "wrong", HttpStatusCode.Unauthorized)]
    [InlineData("manager", "", HttpStatusCode.BadRequest)]
    public async Task InvalidLogin(string username, string password, HttpStatusCode status)
    {
        await using var host = await ApiHost.StartAsync();
        Assert.Equal(status, (await host.LoginAsync(username, password)).StatusCode);
    }

    [Fact]
    public async Task InactiveLoginIsForbiddenAndExistingCookieLosesAccess()
    {
        await using var host = await ApiHost.StartAsync();
        await host.LoginAsync("employee");
        var employee = host.Employees.Employees[1];
        employee.Deactivate(host.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/customers/search?q=test")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.LoginAsync("employee")).StatusCode);
    }

    [Fact]
    public async Task ManagementRequiresManagerAndReflectsDemotion()
    {
        await using var host = await ApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/employees")).StatusCode);
        await host.LoginAsync("employee");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/employees")).StatusCode);
        await host.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/employees")).StatusCode);
        host.Employees.Employees[0].SetManagerStatus(false, host.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/employees")).StatusCode);
    }

    [Fact]
    public async Task ManagerCreatesPromotesResetsAndDeactivates()
    {
        await using var host = await ApiHost.StartAsync();
        await host.LoginAsync();
        var request = new { firstName = "Created", lastName = "Employee", username = "newuser", password = "Test-password-123",
            phone = "0501234567", nationalId = "333333333", isManager = false };
        using var created = await host.Client.PostAsJsonAsync("/api/employees", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var employee = (await created.Content.ReadFromJsonAsync<EmployeeDto>())!;
        Assert.Equal(host.Employees.Employees[0].Id, host.Employees.Employees.Last().CreatedByEmployeeId);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PostAsJsonAsync("/api/employees", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync($"/api/employees/{employee.EmployeeNumber}/manager-status", new { isManager = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsJsonAsync($"/api/employees/{employee.EmployeeNumber}/reset-password", new { newPassword = "Reset-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PatchAsync($"/api/employees/{employee.EmployeeNumber}/deactivate", null)).StatusCode);
        Assert.False(host.Employees.Employees.Last().IsActive);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsync("/api/employees/1/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PatchAsJsonAsync("/api/employees/1/manager-status", new { isManager = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/employees/999")).StatusCode);
    }

    [Fact]
    public async Task OwnPasswordChangeUsesCurrentIdentity()
    {
        await using var host = await ApiHost.StartAsync();
        await host.LoginAsync("employee");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "wrong", newPassword = "New-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "Test-password-123", newPassword = "New-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.LoginAsync("employee", "New-password")).StatusCode);
    }

    [Fact]
    public async Task CustomersRequireAuthenticationAndIgnoreForgedAuditIdentity()
    {
        await using var host = await ApiHost.StartAsync();
        host.Client.DefaultRequestHeaders.Add("X-Employee-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/customers/search?q=Test")).StatusCode);
        await host.LoginAsync("employee");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/customers/search?q=Test")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/customers/42")).StatusCode);
        var actor = host.Employees.Employees[1].Id;
        var request = new { nationalId = "987654321", firstName = "New", lastName = "Customer", dateOfBirth = "2000-01-01",
            mobilePhone = "0501234567", city = "Haifa", gender = "Male", createdByEmployeeId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/customers", request)).StatusCode);
        Assert.Equal(actor, host.Customers.Items.Last().CreatedByEmployeeId);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PutAsJsonAsync("/api/customers/42", request)).StatusCode);
        Assert.Equal(actor, host.Customers.Items[0].UpdatedByEmployeeId);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PatchAsJsonAsync("/api/customers/42/whatsapp-consent", new { consent = true })).StatusCode);
        Assert.Equal(actor, host.Customers.Items[0].UpdatedByEmployeeId);
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PatchAsync("/api/customers/42/deactivate", null)).StatusCode);
        Assert.Equal(actor, host.Customers.Items[0].UpdatedByEmployeeId);
    }

    [Fact]
    public async Task NewValidationReturnsSafeBadRequestsWithoutSaving()
    {
        await using var host = await ApiHost.StartAsync();
        await host.LoginAsync();
        foreach (var field in new[] { "nationalId", "mobilePhone", "homePhone", "gender" })
        {
            var request = new Dictionary<string, object>
            {
                ["nationalId"] = new string('0', 9), ["firstName"] = "First", ["lastName"] = "Last",
                ["dateOfBirth"] = "2000-01-01", ["mobilePhone"] = new string('0', 10), ["homePhone"] = "",
                ["city"] = "City", ["gender"] = "Female"
            };
            request[field] = "invalid-value";
            using var response = await host.Client.PostAsJsonAsync("/api/customers", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.DoesNotContain("invalid-value", await response.Content.ReadAsStringAsync());
            Assert.Single(host.Customers.Items);
            if (field != "nationalId")
            {
                using var update = await host.Client.PutAsJsonAsync("/api/customers/42", request);
                Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
                Assert.Null(host.Customers.Items[0].UpdatedAtUtc);
            }
        }
        foreach (var field in new[] { "nationalId", "phone" })
        {
            var request = new Dictionary<string, object>
            {
                ["firstName"] = "First", ["lastName"] = "Last", ["username"] = "new-user", ["password"] = "Test-fixture-password",
                ["nationalId"] = new string('0', 9), ["phone"] = new string('0', 10), ["isManager"] = false
            };
            request[field] = "invalid-value";
            using var response = await host.Client.PostAsJsonAsync("/api/employees", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.DoesNotContain("invalid-value", await response.Content.ReadAsStringAsync());
            Assert.Equal(2, host.Employees.Employees.Count);
        }
    }

    [Fact]
    public async Task CrossOriginMutationRejected()
    {
        await using var host = await ApiHost.StartAsync();
        host.Client.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.LoginAsync()).StatusCode);
    }

    [Fact]
    public async Task MissingRequiredJsonIsBadRequest()
    {
        await using var host = await ApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/login", new { username = "manager" })).StatusCode);
    }

    private sealed class ApiHost : IAsyncDisposable
    {
        private readonly WebApplication app;
        public HttpClient Client { get; }
        public FakeEmployeeRepository Employees { get; }
        public TestCustomers Customers { get; }
        private ApiHost(WebApplication app, FakeEmployeeRepository employees, TestCustomers customers)
        {
            this.app = app;
            Employees = employees;
            Customers = customers;
            Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
            { BaseAddress = new Uri(app.Urls.Single()) };
        }
        public static async Task<ApiHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddApplication();
            var employees = new FakeEmployeeRepository();
            employees.Seed(); employees.Seed("employee", false, "222222222");
            var customers = new TestCustomers();
            builder.Services.AddSingleton<IEmployeeRepository>(employees);
            builder.Services.AddSingleton<IPasswordHasher, EmployeePasswordHasher>();
            builder.Services.AddSingleton<ICustomerRepository>(customers);
            builder.Services.AddEmployeeAuthentication(true);
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            var app = builder.Build();
            app.UseExceptionHandler();
            app.UseMiddleware<SameOriginRequests>();
            app.UseAuthentication(); app.UseAuthorization();
            app.MapAuthEndpoints(); app.MapEmployeeEndpoints(); app.MapCustomerEndpoints();
            await app.StartAsync();
            return new ApiHost(app, employees, customers);
        }
        public Task<HttpResponseMessage> LoginAsync(string username = "manager", string password = "Test-password-123") =>
            Client.PostAsJsonAsync("/api/auth/login", new { username, password });
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }

    private sealed class TestCustomers : ICustomerRepository
    {
        public List<Customer> Items { get; } = [new("123456789", "Test", "Customer", new DateOnly(2000, 1, 1), "0501234567", "Haifa", "Male", Guid.NewGuid())];
        public TestCustomers() => typeof(Customer).GetProperty(nameof(Customer.CustomerNumber))!.SetValue(Items[0], 42);
        public Task AddAsync(Customer item, CancellationToken ct) { Items.Add(item); return Task.CompletedTask; }
        public Task<bool> NationalIdExistsAsync(string id, CancellationToken ct) => Task.FromResult(Items.Any(e => e.NationalId == id));
        public Task<Customer?> GetByCustomerNumberAsync(int number, bool forUpdate, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(e => e.CustomerNumber == number));
        public Task<Customer?> GetByNationalIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(e => e.NationalId == id));
        public Task<IReadOnlyList<Customer>> SearchAsync(string query, CancellationToken ct) => Task.FromResult<IReadOnlyList<Customer>>(Items.ToArray());
        public Task SaveChangesAsync(CancellationToken ct)
        {
            foreach (var item in Items.Where(e => e.CustomerNumber == 0)) typeof(Customer).GetProperty(nameof(Customer.CustomerNumber))!.SetValue(item, 43);
            return Task.CompletedTask;
        }
    }
}
