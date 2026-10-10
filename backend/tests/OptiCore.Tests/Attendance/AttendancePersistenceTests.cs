using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OptiCore.Infrastructure.Persistence.Migrations;
using OptiCore.Domain.Attendance;
using OptiCore.Infrastructure.Persistence;
using OptiCore.Application.Attendance;
using OptiCore.Infrastructure.Persistence.Repositories;

namespace OptiCore.Tests.Attendance;

public sealed class AttendancePersistenceTests
{
    [Fact]
    public void HistoryProjectionFiltersAndPaginationTranslateToPostgresOffline()
    {
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var repository = new AttendanceRepository(db);
        var from = DateTimeOffset.Parse("2026-07-01T21:00:00Z");
        var query = (IQueryable<AttendanceHistoryDto>)typeof(AttendanceRepository)
            .GetMethod("HistoryQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(repository, [42, from, from.AddDays(1), null, 25, 25])!;
        var sql = query.ToQueryString();
        Assert.Contains("INNER JOIN", sql);
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("NationalId", sql);
        Assert.DoesNotContain("Username", sql);
        Assert.DoesNotContain("Phone", sql);
    }
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

    [Fact]
    public void AdditiveMigrationOnlyChangesAttendanceAndPreservesOpenIndex()
    {
        var migration = new AddAttendanceCorrections();
        Assert.Equal(5, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation =>
        {
            switch (operation)
            {
                case CreateTableOperation table:
                    Assert.Equal("AttendanceCorrections", table.Name);
                    Assert.Equal(9, table.Columns.Count);
                    Assert.Equal(2, table.ForeignKeys.Count);
                    Assert.All(table.ForeignKeys, key => Assert.Equal(Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict, key.OnDelete));
                    break;
                case AddCheckConstraintOperation constraint:
                    Assert.Equal("AttendanceRecords", constraint.Table);
                    Assert.Equal("CK_AttendanceRecords_Checkout", constraint.Name);
                    Assert.DoesNotContain("\"UpdatedByEmployeeId\" IS NULL", constraint.Sql);
                    Assert.Contains("\"CheckOutAtUtc\" <= \"AutomaticCheckoutDueAtUtc\"", constraint.Sql);
                    break;
                case DropCheckConstraintOperation constraint:
                    Assert.Equal("AttendanceRecords", constraint.Table);
                    Assert.Equal("CK_AttendanceRecords_Checkout", constraint.Name);
                    break;
                case CreateIndexOperation index:
                    Assert.Equal("AttendanceCorrections", index.Table);
                    break;
                default: Assert.Fail("Unexpected migration operation: " + operation.GetType().Name); break;
            }
        });
        Assert.DoesNotContain(migration.UpOperations, operation => operation is DropIndexOperation);
    }

    [Fact]
    public void CorrectionModelHasRequiredReasonUtcColumnsAndRestrictedForeignKeys()
    {
        using var db = new OptiCoreDesignTimeDbContextFactory().CreateDbContext([]);
        var model = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(AttendanceCorrection))!;
        Assert.Equal(2, model.GetForeignKeys().Count());
        Assert.All(model.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
        var reason = model.FindProperty(nameof(AttendanceCorrection.Reason))!;
        Assert.False(reason.IsNullable);
        Assert.Equal(2000, reason.GetMaxLength());
        foreach (var name in new[] { "PreviousCheckInAtUtc", "PreviousCheckOutAtUtc", "NewCheckInAtUtc", "NewCheckOutAtUtc", "CorrectedAtUtc" })
            Assert.Equal("timestamp with time zone", model.FindProperty(name)!.GetColumnType());
        Assert.Equal(2, model.GetCheckConstraints().Count());
        Assert.Null(db.Database.GetConnectionString());
    }
}
