using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
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
    [Theory]
    [InlineData("employee", 3)]
    [InlineData("manager", 1)]
    public async Task ActiveEmployeesAndManagersUseSubjectNumberIndependentlyOfActor(string username, int actorNumber)
    {
        await using var fixture = await Fixture.Start();
        await fixture.Login(username);
        var client = fixture.Client;
        using var entered = await client.PostAsync("/api/attendance/2/check-in", null);
        Assert.Equal(HttpStatusCode.OK, entered.StatusCode);
        var dto = (await entered.Content.ReadFromJsonAsync<AttendanceDto>())!;
        Assert.Equal(fixture.Employees.Employees[1].Id, dto.EmployeeId);
        Assert.Equal(fixture.Employees.Employees[actorNumber - 1].Id, dto.CreatedByEmployeeId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/attendance/2/check-in", null)).StatusCode);
        using var exited = await client.PostAsync("/api/attendance/2/check-out", null);
        Assert.Equal(HttpStatusCode.OK, exited.StatusCode);
        var closed = (await exited.Content.ReadFromJsonAsync<AttendanceDto>())!;
        Assert.Equal(fixture.Employees.Employees[actorNumber - 1].Id, closed.UpdatedByEmployeeId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/attendance/2/check-out", null)).StatusCode);
        fixture.Employees.Employees[1].Deactivate(fixture.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/attendance/2/check-in", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AnonymousRequestsReturn401AndNoDeleteEndpointsExist()
    {
        await using var fixture = await Fixture.Start();
        var recordPath = "/api/attendance/management/records/" + Guid.NewGuid();
        foreach (var path in new[] { "/api/attendance/2/status", "/api/attendance/management/records", recordPath })
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(path)).StatusCode);
        foreach (var path in new[] { "/api/attendance/2/check-in", "/api/attendance/2/check-out", recordPath + "/corrections" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync(path, new { })).StatusCode);
        var attendanceRoutes = ((IEndpointRouteBuilder)fixture.App).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/attendance"));
        Assert.NotEmpty(attendanceRoutes);
        Assert.All(attendanceRoutes, endpoint => Assert.DoesNotContain("DELETE", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
        await fixture.Login("manager");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await fixture.Client.DeleteAsync(recordPath)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await fixture.Client.DeleteAsync(recordPath + "/corrections")).StatusCode);
    }

    [Fact]
    public async Task StatusReturnsOnlySafeFieldsAndEffectiveMidnightState()
    {
        await using var fixture = await Fixture.Start();
        await fixture.Login("employee");
        using var response = await fixture.Client.GetAsync("/api/attendance/2/status");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "checkInAtUtc", "employeeNumber", "firstName", "hasOpenAttendance", "lastName" },
            json.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.False(json.GetProperty("hasOpenAttendance").GetBoolean());
        await fixture.Client.PostAsync("/api/attendance/2/check-in", null);
        var current = (await fixture.Client.GetFromJsonAsync<AttendanceStatusDto>("/api/attendance/2/status"))!;
        Assert.True(current.HasOpenAttendance);
        Assert.Equal(fixture.Clock.Now, current.CheckInAtUtc);
        fixture.Clock.Now = fixture.Records.Rows[0].AutomaticCheckoutDueAtUtc;
        var midnight = (await fixture.Client.GetFromJsonAsync<AttendanceStatusDto>("/api/attendance/2/status"))!;
        Assert.False(midnight.HasOpenAttendance);
        Assert.Null(midnight.CheckInAtUtc);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/attendance/999/status")).StatusCode);
    }

    [Fact]
    public async Task RegularEmployeeCannotAccessHistoryDetailsOrCorrectionsAndInactiveLoginIsRejected()
    {
        await using var fixture = await Fixture.Start();
        await fixture.Login("employee");
        var path = "/api/attendance/management/records/" + Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/attendance/management/records")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync(path + "/corrections", new { })).StatusCode);
        fixture.Employees.Employees[2].Deactivate(fixture.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsync("/api/attendance/2/check-in", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsync("/api/attendance/2/check-out", null)).StatusCode);
    }

    [Fact]
    public async Task ManagerHistoryFiltersDetailsCorrectionValidationAuditAndRoleRevalidation()
    {
        await using var fixture = await Fixture.Start();
        await fixture.Login("manager");
        var client = fixture.Client;
        await client.PostAsync("/api/attendance/2/check-in", null);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(2);
        var row = fixture.Records.Rows[0];
        var start = row.CheckInAtUtc;
        var path = $"/api/attendance/management/records/{row.Id}";
        var history = (await client.GetFromJsonAsync<AttendanceHistoryPage>("/api/attendance/management/records?employeeNumber=2&from=2026-07-01&to=2026-07-01"))!;
        Assert.Single(history.Items);
        Assert.Equal(row.Id, history.Items[0].Id);
        Assert.Empty((await client.GetFromJsonAsync<AttendanceHistoryPage>("/api/attendance/management/records?employeeNumber=1"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<AttendanceHistoryPage>("/api/attendance/management/records?from=2026-07-02"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/attendance/management/records?from=2026-07-02&to=2026-07-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/attendance/management/records?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/attendance/management/records/" + Guid.NewGuid())).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<AttendanceDetailsDto>(path))!.Corrections);
        CorrectAttendanceRequest Request(DateTimeOffset? end, string reason) => new(start, end, reason, start, null, null);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/corrections", Request(fixture.Clock.Now, " "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/corrections", Request(start.AddMinutes(-1), "תיקון"))).StatusCode);
        Assert.Null(row.CheckOutAtUtc);
        Assert.Empty(fixture.Records.Corrections);
        var request = Request(fixture.Clock.Now.AddHours(-1), "יציאה חסרה");
        using var response = await client.PostAsJsonAsync(path + "/corrections", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = (await response.Content.ReadFromJsonAsync<AttendanceDetailsDto>())!;
        var correction = Assert.Single(details.Corrections);
        Assert.Null(correction.PreviousCheckOutAtUtc);
        Assert.Equal(request.CheckOutAtUtc, correction.NewCheckOutAtUtc);
        Assert.Equal(start, correction.PreviousCheckInAtUtc);
        Assert.Equal(start, correction.NewCheckInAtUtc);
        Assert.Equal(1, correction.CorrectedByEmployeeNumber);
        Assert.Equal(fixture.Clock.Now, correction.CorrectedAtUtc);
        Assert.Equal("יציאה חסרה", correction.Reason);
        Assert.Equal(fixture.Employees.Employees[0].Id, fixture.Records.Corrections[0].CorrectedByEmployeeId);
        Assert.Single((await client.GetFromJsonAsync<AttendanceDetailsDto>(path))!.Corrections);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/corrections", request)).StatusCode);
        fixture.Employees.Employees[0].SetManagerStatus(false, fixture.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/corrections", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/attendance/1/check-in", null)).StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required WebApplication App { get; init; }
        public required HttpClient Client { get; init; }
        public required FakeEmployeeRepository Employees { get; init; }
        public required FakeAttendanceRepository Records { get; init; }
        public required TestClock Clock { get; init; }
        public async Task Login(string username)
        {
            using var response = await Client.PostAsJsonAsync("/api/auth/login", new { username, password = "Test-password-123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        public static async Task<Fixture> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddApplication();
            var clock = new TestClock(DateTimeOffset.Parse("2026-07-01T10:00:00Z"));
            builder.Services.AddSingleton<TimeProvider>(clock);
            var employees = new FakeEmployeeRepository();
            employees.Seed();
            employees.Seed("subject", false, "222222222");
            employees.Seed("employee", false, "333333333");
            var records = new FakeAttendanceRepository(employees);
            builder.Services.AddSingleton<IEmployeeRepository>(employees);
            builder.Services.AddSingleton<IEmployeePermissionRepository>(new FakeEmployeePermissionRepository(employees));
            builder.Services.AddSingleton<IPasswordHasher, EmployeePasswordHasher>();
            builder.Services.AddSingleton<IAttendanceRepository>(records);
            builder.Services.AddEmployeeAuthentication(true);
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            var app = builder.Build();
            app.UseExceptionHandler(); app.UseMiddleware<SameOriginRequests>(); app.UseAuthentication(); app.UseAuthorization();
            app.MapAuthEndpoints(); app.MapAttendanceEndpoints();
            await app.StartAsync();
            return new() { App = app, Client = new HttpClient(new HttpClientHandler { CookieContainer = new() }) { BaseAddress = new Uri(app.Urls.Single()) },
                Employees = employees, Records = records, Clock = clock };
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await App.StopAsync(); await App.DisposeAsync(); }
    }
}
