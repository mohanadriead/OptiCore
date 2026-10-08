using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OptiCore.Domain.Employees;
using OptiCore.Domain.Permissions;
using OptiCore.Infrastructure.Persistence;
using OptiCore.Infrastructure.Persistence.Repositories;
using OptiCore.Infrastructure.Persistence.Migrations;

namespace OptiCore.Tests.Permissions;

public sealed class PermissionPersistenceTests
{
    [Fact]
    public void MigrationOnlyCreatesPermissionAssignmentsAndDoesNotRewriteExistingTables()
    {
        var migration = new AddEmployeePermissions();
        var table = Assert.IsType<CreateTableOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("EmployeePermissions", table.Name);
        Assert.Equal(new[] { "EmployeeId", "PermissionCode" }, table.Columns.Select(column => column.Name));
        Assert.Equal(new[] { "EmployeeId", "PermissionCode" }, table.PrimaryKey!.Columns);
        Assert.Single(table.CheckConstraints);
        Assert.Equal("Employees", Assert.Single(table.ForeignKeys).PrincipalTable);
        Assert.Equal("EmployeePermissions", Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations)).Name);
    }
    [Fact]
    public void OfflineModelHasCompositeKeyEmployeeGuidForeignKeyAndKnownCodeConstraint()
    {
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(EmployeePermission))!;
        Assert.Equal(new[] { "EmployeeId", "PermissionCode" }, entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(64, entity.FindProperty("PermissionCode")!.GetMaxLength());
        var foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Employee), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal("Id", Assert.Single(foreignKey.PrincipalKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        var constraint = Assert.Single(entity.GetCheckConstraints());
        foreach (var code in PermissionCatalog.Codes) Assert.Contains($"'{code}'", constraint.Sql);
        Assert.Null(db.Database.GetConnectionString());
    }

    [Fact]
    public async Task RepositoryRejectsReplacementOutsideTransactionWithoutConnecting()
    {
        await using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var repository = new EmployeePermissionRepository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReplaceAsync(Guid.NewGuid(), [PermissionCatalog.ViewReports], default));
    }
}
