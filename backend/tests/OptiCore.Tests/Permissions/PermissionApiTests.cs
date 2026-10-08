using System.Net;
using System.Net.Http.Json;
using OptiCore.Application.Permissions;
using OptiCore.Domain.Permissions;
using OptiCore.Tests.Employees;

namespace OptiCore.Tests.Permissions;

public sealed class PermissionApiTests
{
    [Theory]
    [InlineData("/api/permissions")]
    [InlineData("/api/employees/2/permissions")]
    public async Task AdministrationRequiresAuthenticatedManager(string path)
    {
        await using var host = await AuthenticationApiTests.ApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PutAsJsonAsync("/api/employees/2/permissions", new { permissions = new[] { "ViewReports" } })).StatusCode);
        await host.LoginAsync("employee");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PutAsJsonAsync("/api/employees/2/permissions", new { permissions = new[] { "ViewReports" } })).StatusCode);
        Assert.Empty((await host.Client.GetFromJsonAsync<string[]>("/api/auth/permissions"))!);
        await host.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task ReplacementAndCurrentEffectiveEndpointReflectDatabaseStateWithoutRelogin()
    {
        await using var host = await AuthenticationApiTests.ApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/permissions")).StatusCode);
        await host.LoginAsync();
        Assert.Equal(PermissionCatalog.Codes, (await host.Client.GetFromJsonAsync<string[]>("/api/auth/permissions"))!);
        var catalog = (await host.Client.GetFromJsonAsync<PermissionDto[]>("/api/permissions"))!;
        Assert.Equal(PermissionCatalog.Codes, catalog.Select(item => item.Code));
        using var put = await host.Client.PutAsJsonAsync("/api/employees/2/permissions", new { permissions = new[] { "ViewReports", "ViewReports", "ExportData" } });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var result = (await put.Content.ReadFromJsonAsync<EmployeePermissionsDto>())!;
        Assert.Equal(2, result.AssignedPermissions.Count);
        Assert.Equal(result.AssignedPermissions, result.EffectivePermissions);
        await host.LoginAsync("employee");
        Assert.Equal(new[] { "ExportData", "ViewReports" }, (await host.Client.GetFromJsonAsync<string[]>("/api/auth/permissions"))!.Order());
        // A role change is observed through existing cookie validation; no permission claims added.
        host.Employees.Employees[1].SetManagerStatus(true, host.Employees.Employees[0].Id);
        Assert.Equal(PermissionCatalog.Codes, (await host.Client.GetFromJsonAsync<string[]>("/api/auth/permissions"))!);
        host.Employees.Employees[1].SetManagerStatus(false, host.Employees.Employees[0].Id);
        Assert.Equal(2, (await host.Client.GetFromJsonAsync<string[]>("/api/auth/permissions"))!.Length);
        host.Employees.Employees[1].Deactivate(host.Employees.Employees[0].Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/permissions")).StatusCode);
    }

    [Fact]
    public async Task ManagerTargetCannotLoseEffectivePermissionsAndInvalidInputDoesNotReplace()
    {
        await using var host = await AuthenticationApiTests.ApiHost.StartAsync();
        await host.LoginAsync();
        await host.Client.PutAsJsonAsync("/api/employees/1/permissions", new { permissions = new[] { "ViewReports" } });
        var manager = (await host.Client.GetFromJsonAsync<EmployeePermissionsDto>("/api/employees/1/permissions"))!;
        Assert.Single(manager.AssignedPermissions);
        Assert.Equal(PermissionCatalog.Codes, manager.EffectivePermissions);
        using var invalid = await host.Client.PutAsJsonAsync("/api/employees/1/permissions", new { permissions = new[] { "private-invalid-code" } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var text = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Unknown permission code.", text);
        Assert.DoesNotContain("private-invalid-code", text);
        Assert.Equal(manager.AssignedPermissions, (await host.Client.GetFromJsonAsync<EmployeePermissionsDto>("/api/employees/1/permissions"))!.AssignedPermissions);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/employees/999/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PutAsJsonAsync("/api/employees/999/permissions", new { permissions = Array.Empty<string>() })).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"permissions\":null}")]
    [InlineData("{\"permissions\":[null]}")]
    public async Task MalformedPermissionRequestsAreBadRequests(string json)
    {
        await using var host = await AuthenticationApiTests.ApiHost.StartAsync();
        await host.LoginAsync();
        using var response = await host.Client.PutAsync("/api/employees/2/permissions", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
