using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OptiCore.Infrastructure.Persistence.Migrations;
using OptiCore.Domain.Attendance;
using OptiCore.Infrastructure.Persistence;

namespace OptiCore.Tests.Attendance;

public sealed class AttendancePersistenceTests
{
    [Fact]
    public void OfflineModelEnforcesOneOpenSessionAndEmployeeForeignKey()
    {
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var model = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(AttendanceRecord))!;
        var index = Assert.Single(model.GetIndexes(), index => index.IsUnique);
        Assert.Equal("EmployeeId", Assert.Single(index.Properties).Name);
        Assert.Equal("\"CheckOutAtUtc\" IS NULL", index.GetFilter());
        var fk = Assert.Single(model.GetForeignKeys());
        Assert.Equal("Id", Assert.Single(fk.PrincipalKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Equal(2, model.GetCheckConstraints().Count());
        Assert.Null(db.Database.GetConnectionString());
    }

    [Fact]
    public void MigrationOnlyAddsAttendanceAndFilteredIndexes()
    {
        var migration = new AddAttendance();
        var table = Assert.IsType<CreateTableOperation>(migration.UpOperations[0]);
        Assert.Equal("AttendanceRecords", table.Name);
        Assert.Equal("Employees", Assert.Single(table.ForeignKeys).PrincipalTable);
        Assert.All(migration.UpOperations.Skip(1), operation => Assert.IsType<CreateIndexOperation>(operation));
        var unique = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>(), index => index.IsUnique);
        Assert.Equal("\"CheckOutAtUtc\" IS NULL", unique.Filter);
        Assert.Equal("AttendanceRecords", Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations)).Name);
    }
}
