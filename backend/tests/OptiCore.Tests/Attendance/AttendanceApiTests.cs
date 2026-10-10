using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OptiCore.Api.Endpoints.Attendance;
using OptiCore.Api.Endpoints.Auth;
using OptiCore.Api.Errors;
using OptiCore.Api.Security;
using OptiCore.Application;
using OptiCore.Application.Attendance;
using OptiCore.Application.Employees;
using OptiCore.Application.Permissions;
using OptiCore.Infrastructure.Security;
using OptiCore.Tests.Employees;
using OptiCore.Tests.Permissions;

namespace OptiCore.Tests.Attendance;

public sealed class AttendanceApiTests
{
    [Fact]
    public async Task MutationsRequireManagerAndUseSubjectNumberWithSafeHebrewConflicts()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddApplication();
        builder.Services.AddSingleton<TimeProvider>(new TestClock(DateTimeOffset.Parse("2026-07-01T10:00:00Z")));
        var employees = new FakeEmployeeRepository();
        var manager = employees.Seed();
        var subject = employees.Seed("employee", false, "222222222");
        builder.Services.AddSingleton<IEmployeeRepository>(employees);
        builder.Services.AddSingleton<IEmployeePermissionRepository>(new FakeEmployeePermissionRepository(employees));
        builder.Services.AddSingleton<IPasswordHasher, EmployeePasswordHasher>();
        builder.Services.AddSingleton<IAttendanceRepository>(new FakeAttendanceRepository());
        builder.Services.AddEmployeeAuthentication(true);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseMiddleware<SameOriginRequests>();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapAuthEndpoints(); app.MapAttendanceEndpoints();
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new() }) { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (var action in new[] { "check-in", "check-out" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/attendance/2/{action}", null)).StatusCode);
        await client.PostAsJsonAsync("/api/auth/login", new { username = "employee", password = "Test-password-123" });
        foreach (var action in new[] { "check-in", "check-out" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/attendance/2/{action}", null)).StatusCode);
        await client.PostAsJsonAsync("/api/auth/login", new { username = "manager", password = "Test-password-123" });
        using var entered = await client.PostAsync("/api/attendance/2/check-in", null);
        Assert.Equal(HttpStatusCode.OK, entered.StatusCode);
        var dto = (await entered.Content.ReadFromJsonAsync<AttendanceDto>())!;
        Assert.Equal(subject.Id, dto.EmployeeId);
        Assert.Equal(manager.Id, dto.CreatedByEmployeeId);
        using var duplicate = await client.PostAsync("/api/attendance/2/check-in", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("לעובד כבר קיימת כניסה פתוחה.", (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        subject.Deactivate(manager.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/attendance/2/check-out", null)).StatusCode);
        using var missing = await client.PostAsync("/api/attendance/2/check-out", null);
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Equal("לא קיימת כניסה פתוחה לעובד.", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/attendance/2/check-in", null)).StatusCode);
        manager.SetManagerStatus(false, manager.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/attendance/1/check-in", null)).StatusCode);
        await app.StopAsync();
    }
}
