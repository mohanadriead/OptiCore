using OptiCore.Application.Employees;
using OptiCore.Application.Permissions;
using OptiCore.Domain.Permissions;
using OptiCore.Infrastructure.Security;
using OptiCore.Tests.Employees;

namespace OptiCore.Tests.Permissions;

public sealed class PermissionServiceTests
{
    private readonly FakeEmployeeRepository employees = new();
    private readonly FakeEmployeePermissionRepository assignments;
    private readonly PermissionService service;
    private readonly Guid manager;
    private readonly Guid regular;
    public PermissionServiceTests()
    {
        manager = employees.Seed().Id;
        regular = employees.Seed("regular", false, new string('0', 9)).Id;
        assignments = new(employees);
        service = new(employees, assignments);
    }

    [Fact]
    public async Task RegularEmployeeStartsWithoutConfigurablePermissions()
    {
        Assert.Empty(await service.GetEffectivePermissionsAsync(regular, default));
        foreach (var code in PermissionCatalog.Codes) Assert.False(await service.HasPermissionAsync(regular, code, default));
    }

    [Fact]
    public async Task RegularEmployeeHasOnlyAssignedPermissionsAndReplacementDeduplicates()
    {
        await service.SetAssignedPermissionsAsync(2, [PermissionCatalog.ReceiveStock, PermissionCatalog.ReceiveStock, PermissionCatalog.ViewReports], manager, default);
        Assert.Equal(2, (await assignments.GetAssignedAsync(regular, default)).Count);
        Assert.True(await service.HasPermissionAsync(regular, PermissionCatalog.ReceiveStock, default));
        Assert.False(await service.HasPermissionAsync(regular, PermissionCatalog.ViewProfit, default));
        var updated = await service.SetAssignedPermissionsAsync(2, [PermissionCatalog.ExportData], manager, default);
        Assert.Equal([PermissionCatalog.ExportData], updated.AssignedPermissions);
        Assert.Equal(updated.AssignedPermissions, updated.EffectivePermissions);
        Assert.False(await service.HasPermissionAsync(regular, PermissionCatalog.ReceiveStock, default));
        Assert.True(await service.HasPermissionAsync(regular, PermissionCatalog.ExportData, default));
        await service.SetAssignedPermissionsAsync(2, [], manager, default);
        Assert.Empty(await service.GetEffectivePermissionsAsync(regular, default));
    }

    [Fact]
    public async Task ManagerHasEveryPermissionWithoutAssignmentsAndWithPartialAssignments()
    {
        Assert.Empty(await assignments.GetAssignedAsync(manager, default));
        Assert.Equal(PermissionCatalog.Codes, await service.GetEffectivePermissionsAsync(manager, default));
        foreach (var code in PermissionCatalog.Codes) Assert.True(await service.HasPermissionAsync(manager, code, default));
        var result = await service.SetAssignedPermissionsAsync(1, [PermissionCatalog.GiveDiscount], manager, default);
        Assert.Equal([PermissionCatalog.GiveDiscount], result.AssignedPermissions);
        Assert.Equal(PermissionCatalog.Codes, result.EffectivePermissions);
        Assert.True(result.IsManager);
    }

    [Fact]
    public async Task PromotionAndDemotionPreserveAssignmentsAndLastManagerGuard()
    {
        await service.SetAssignedPermissionsAsync(2, [PermissionCatalog.ViewReports], manager, default);
        var employeeService = new EmployeeService(employees, new EmployeePasswordHasher());
        await employeeService.SetManagerStatusAsync(2, true, manager, default);
        Assert.Equal(PermissionCatalog.Codes, await service.GetEffectivePermissionsAsync(regular, default));
        Assert.Equal([PermissionCatalog.ViewReports], await assignments.GetAssignedAsync(regular, default));
        await employeeService.SetManagerStatusAsync(2, false, manager, default);
        Assert.Equal([PermissionCatalog.ViewReports], await service.GetEffectivePermissionsAsync(regular, default));
        await Assert.ThrowsAsync<LastActiveManagerException>(() => employeeService.SetManagerStatusAsync(1, false, manager, default));
    }

    [Fact]
    public async Task CatalogIsExactAndExcludesManagerOnlyCapabilities()
    {
        var catalog = await service.GetCatalogAsync(manager, default);
        Assert.Equal(new[] { "ReceiveStock", "GiveDiscount", "ViewDailySales", "ViewProfit", "ViewSupplierDetails", "ViewReports", "ExportData", "ViewAuditLogs" }, catalog.Select(item => item.Code));
        Assert.All(catalog, item => Assert.False(string.IsNullOrWhiteSpace(item.Label)));
        Assert.False(await service.HasPermissionAsync(manager, "AdjustInventory", default));
        Assert.False(await service.HasPermissionAsync(manager, "ManageEmployees", default));
    }

    [Theory]
    [InlineData("Viewreports")]
    [InlineData("AdjustInventory")]
    [InlineData("private-invalid-value")]
    [InlineData("")]
    [InlineData(null)]
    public async Task InvalidCodesAreSafeAndDoNotChangeAssignments(string? invalid)
    {
        await service.SetAssignedPermissionsAsync(2, [PermissionCatalog.ViewReports], manager, default);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.SetAssignedPermissionsAsync(2, [PermissionCatalog.ExportData, invalid!], manager, default));
        Assert.Equal("Unknown permission code.", error.Message);
        Assert.Equal([PermissionCatalog.ViewReports], await assignments.GetAssignedAsync(regular, default));
    }

    [Fact]
    public async Task MissingSetAndMissingTargetAreRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetAssignedPermissionsAsync(2, null, manager, default));
        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => service.GetAssignedPermissionsAsync(999, manager, default));
        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => service.SetAssignedPermissionsAsync(999, [], manager, default));
    }

    [Fact]
    public async Task RegularEmployeeCannotAdministerEvenTheirOwnPermissions()
    {
        await Assert.ThrowsAsync<ManagerRequiredException>(() => service.GetCatalogAsync(regular, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => service.GetAssignedPermissionsAsync(2, regular, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => service.SetAssignedPermissionsAsync(2, [PermissionCatalog.ViewReports], regular, default));
        Assert.Empty(await assignments.GetAssignedAsync(regular, default));
    }

    [Fact]
    public async Task InactiveOrMissingActorsNeverHaveAccess()
    {
        employees.Employees[0].Deactivate(manager);
        Assert.False(await service.HasPermissionAsync(manager, PermissionCatalog.ViewReports, default));
        Assert.False(await service.HasPermissionAsync(Guid.NewGuid(), PermissionCatalog.ViewReports, default));
        await Assert.ThrowsAsync<InactiveEmployeeException>(() => service.GetEffectivePermissionsAsync(manager, default));
        await Assert.ThrowsAsync<InactiveEmployeeException>(() => service.SetAssignedPermissionsAsync(2, [], manager, default));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.SetAssignedPermissionsAsync(2, [], Guid.NewGuid(), default));
    }

    [Fact]
    public async Task AdministrationReadsBothAssignedAndEffectiveSets()
    {
        await service.SetAssignedPermissionsAsync(2, [PermissionCatalog.ViewReports], manager, default);
        var result = await service.GetAssignedPermissionsAsync(2, manager, default);
        Assert.Equal(2, result.EmployeeNumber);
        Assert.False(result.IsManager);
        Assert.Equal([PermissionCatalog.ViewReports], result.AssignedPermissions);
        Assert.Equal(result.AssignedPermissions, result.EffectivePermissions);
    }

    [Fact]
    public void AssignmentEntityRejectsEmptyEmployeeAndUnknownCode()
    {
        Assert.Throws<ArgumentException>(() => new EmployeePermission(Guid.Empty, PermissionCatalog.ViewReports));
        Assert.Throws<ArgumentException>(() => new EmployeePermission(regular, "unknown"));
    }
}
