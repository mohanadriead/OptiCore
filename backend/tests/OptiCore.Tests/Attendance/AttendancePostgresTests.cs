using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OptiCore.Application.Attendance;
using OptiCore.Domain.Attendance;
using OptiCore.Domain.Employees;
using OptiCore.Infrastructure.Persistence;
using OptiCore.Infrastructure.Persistence.Repositories;
using OptiCore.Tests.Employees;

namespace OptiCore.Tests.Attendance;

public sealed class AttendancePostgresTests
{
    [DevelopmentDatabaseFact]
    public async Task PostgresIndexAndTransactionsProtectConcurrentAttendanceAndRecovery()
    {
        var config = new ConfigurationBuilder().AddUserSecrets("53e5fb15-24d8-4ab8-966c-c2f98aa7a918").AddEnvironmentVariables().Build();
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("OptiCoreDatabase"));
        Assert.Equal("opticore_dev", connection.Database);
        connection.IncludeErrorDetail = false;
        connection.Pooling = false;
        var schema = "attendance_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin).ExecuteNonQueryAsync();
        try
        {
            connection.SearchPath = schema;
            var options = new DbContextOptionsBuilder<OptiCoreDbContext>().UseNpgsql(connection.ConnectionString).Options;
            var actor = new Employee("Test", "Manager", "manager", "fixture-hash", "0500000000", "111111111", true, null);
            var subject = new Employee("Test", "Subject", "subject", "fixture-hash", "0500000000", "222222222", false, null);
            await using (var db = new OptiCoreDbContext(options))
            {
                // Only the isolated test schema is created; no migrations are applied.
                await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
                db.Employees.AddRange(actor, subject);
                await db.SaveChangesAsync();
            }
            var clock = new TestClock(DateTimeOffset.Parse("2026-07-01T10:00:00Z"));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ct = timeout.Token;
            // Independent writers bypass application locking and race against the filtered index.
            async Task<bool> DirectEntry()
            {
                await using var db = new OptiCoreDbContext(options);
                var repository = new AttendanceRepository(db);
                await repository.AddAsync(new AttendanceRecord(actor.Id, actor.Id, clock.Now, new AttendanceMidnightPolicy().NextMidnightUtc(clock.Now)), ct);
                try { await repository.SaveChangesAsync(ct); return true; }
                catch (DuplicateAttendanceException) { return false; }
            }
            var directEntries = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DirectEntry()));
            Assert.Single(directEntries, success => success);
            async Task<bool> Enter()
            {
                await using var db = new OptiCoreDbContext(options);
                var service = new AttendanceService(new AttendanceRepository(db), new EmployeeRepository(db), clock, new());
                try { await service.CheckInAsync(subject.EmployeeNumber, actor.Id, ct); return true; }
                catch (DuplicateAttendanceException) { return false; }
            }
            var entries = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Enter()));
            Assert.Single(entries, success => success);

            // Bypass the service lock: the database index must still reject duplicates.
            await using (var db = new OptiCoreDbContext(options))
            {
                var repository = new AttendanceRepository(db);
                await repository.AddAsync(new AttendanceRecord(subject.Id, actor.Id, clock.Now, new AttendanceMidnightPolicy().NextMidnightUtc(clock.Now)), ct);
                await Assert.ThrowsAsync<DuplicateAttendanceException>(() => repository.SaveChangesAsync(ct));
            }
            async Task<bool> Exit()
            {
                await using var db = new OptiCoreDbContext(options);
                var service = new AttendanceService(new AttendanceRepository(db), new EmployeeRepository(db), clock, new());
                try { await service.CheckOutAsync(subject.EmployeeNumber, actor.Id, ct); return true; }
                catch (NoOpenAttendanceException) { return false; }
            }
            var exits = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Exit()));
            Assert.Single(exits, success => success);
            Assert.True(await Enter());
            clock.Now = clock.Now.AddDays(2);
            async Task<int> Recover()
            {
                await using var db = new OptiCoreDbContext(options);
                return await new AttendanceService(new AttendanceRepository(db), new EmployeeRepository(db), clock, new()).RecoverAsync(ct);
            }
            await Task.WhenAll(Recover(), Recover(), Exit());
            await using var verification = new OptiCoreDbContext(options);
            var rows = await verification.AttendanceRecords.Where(row => row.EmployeeId == subject.Id).ToListAsync(ct);
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.NotNull(row.CheckOutAtUtc));
            var automatic = Assert.Single(rows, row => row.WasCheckoutAutomatic);
            Assert.Equal(automatic.AutomaticCheckoutDueAtUtc, automatic.CheckOutAtUtc);
            Assert.Equal(clock.Now, automatic.CheckoutProcessedAtUtc);
            Assert.Null(automatic.UpdatedByEmployeeId);
            Assert.Equal(0, await verification.AttendanceRecords.CountAsync(row => row.CheckOutAtUtc == null, ct));

            // Exercise real SQL projections and transactional correction audit in the isolated schema.
            await using (var db = new OptiCoreDbContext(options))
            {
                var repository = new AttendanceRepository(db);
                var service = new AttendanceService(repository, new EmployeeRepository(db), clock, new());
                var history = await service.HistoryAsync(subject.EmployeeNumber, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3), 1, 25, actor.Id, ct);
                Assert.Equal(2, history.Total);
                var request = new CorrectAttendanceRequest(automatic.CheckInAtUtc, automatic.AutomaticCheckoutDueAtUtc.AddHours(-1),
                    "יציאה שגויה", automatic.CheckInAtUtc, automatic.CheckOutAtUtc, automatic.UpdatedAtUtc);
                var corrected = await service.CorrectAsync(automatic.Id, request, actor.Id, ct);
                Assert.False(corrected.Record.WasCheckoutAutomatic);
                Assert.Single(corrected.Corrections);
                Assert.Equal(clock.Now, corrected.Corrections[0].CorrectedAtUtc);
            }
            // A failure after SaveChanges rolls back both the record and its new audit entry.
            await using (var db = new OptiCoreDbContext(options))
            {
                var repository = new AttendanceRepository(db);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ExecuteExclusiveAsync<int>(async () =>
                {
                    var row = (await repository.GetByIdAsync(automatic.Id, ct))!;
                    var audit = row.Correct(row.CheckInAtUtc.AddMinutes(-1), row.CheckOutAtUtc,
                        row.AutomaticCheckoutDueAtUtc, actor.Id, clock.Now, "rollback test");
                    await repository.AddCorrectionAsync(audit, ct);
                    await repository.SaveChangesAsync(ct);
                    throw new InvalidOperationException("Injected transaction failure");
                }, ct));
            }
            await using (var db = new OptiCoreDbContext(options))
            {
                Assert.Single(await db.AttendanceCorrections.Where(row => row.AttendanceRecordId == automatic.Id).ToListAsync(ct));
                var row = await db.AttendanceRecords.SingleAsync(row => row.Id == automatic.Id, ct);
                Assert.Equal(automatic.CheckInAtUtc, row.CheckInAtUtc);
            }
        }
        finally
        {
            await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin).ExecuteNonQueryAsync();
        }
    }
}
