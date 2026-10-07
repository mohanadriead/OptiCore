using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OptiCore.Application.Employees;
using OptiCore.Domain.Employees;
using OptiCore.Infrastructure.Persistence;
using OptiCore.Infrastructure.Persistence.Repositories;
using OptiCore.Infrastructure.Security;

namespace OptiCore.Tests.Employees;

public sealed class DevelopmentDatabaseFactAttribute : FactAttribute
{
    public DevelopmentDatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("OPTICORE_VERIFY_DATABASE") != "1")
            Skip = "Opt-in PostgreSQL verification: set OPTICORE_VERIFY_DATABASE=1; uses an isolated temporary schema in opticore_dev.";
    }
}

public sealed class EmployeePostgresTests
{
    [DevelopmentDatabaseFact]
    public async Task AppliedMigrationHasExpectedPublicConstraints()
    {
        var config = new ConfigurationBuilder().AddUserSecrets("53e5fb15-24d8-4ab8-966c-c2f98aa7a918").AddEnvironmentVariables().Build();
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("OptiCoreDatabase"));
        Assert.Equal("opticore_dev", connection.Database);
        connection.IncludeErrorDetail = false;
        await using var db = new OptiCoreDbContext(new DbContextOptionsBuilder<OptiCoreDbContext>().UseNpgsql(connection.ConnectionString).Options);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains("20261007083241_AddEmployees", await db.Database.GetAppliedMigrationsAsync());
        await using var sql = new NpgsqlConnection(connection.ConnectionString);
        await sql.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Employees' AND indexdef LIKE 'CREATE UNIQUE INDEX%' ORDER BY indexname", sql);
        var names = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        Assert.Equal(new[] { "IX_Employees_EmployeeNumber", "IX_Employees_NationalId", "IX_Employees_NormalizedUsername", "PK_Employees" }, names);
        await using var identity = new NpgsqlCommand("SELECT identity_generation FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Employees' AND column_name = 'EmployeeNumber'", sql);
        Assert.Equal("ALWAYS", await identity.ExecuteScalarAsync());
    }

    [DevelopmentDatabaseFact]
    public async Task IdentityUniqueConstraintsAndConcurrentManagerRemoval()
    {
        var config = new ConfigurationBuilder().AddUserSecrets("53e5fb15-24d8-4ab8-966c-c2f98aa7a918").AddEnvironmentVariables().Build();
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("OptiCoreDatabase"));
        Assert.Equal("opticore_dev", connection.Database);
        connection.IncludeErrorDetail = false;
        connection.Pooling = false;
        var schema = "employee_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        // Identifier is entirely generated from a fixed prefix and hex GUID, never caller input.
        await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin).ExecuteNonQueryAsync();
        try
        {
            connection.SearchPath = schema;
            var options = new DbContextOptionsBuilder<OptiCoreDbContext>().UseNpgsql(connection.ConnectionString).Options;
            await using (var db = new OptiCoreDbContext(options))
                await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());

            var hasher = new EmployeePasswordHasher();
            var first = new Employee("Test", "One", "manager-one", hasher.Hash("Test-fixture-password"), "0500000000", "111111111", true, null);
            var second = new Employee("Test", "Two", "manager-two", hasher.Hash("Test-fixture-password"), "0500000000", "222222222", true, null);
            await using (var db = new OptiCoreDbContext(options))
            {
                var repository = new EmployeeRepository(db);
                await repository.ExecuteExclusiveAsync(async () =>
                {
                    await repository.AddAsync(first, default);
                    await repository.AddAsync(second, default);
                    await repository.SaveChangesAsync(default);
                    return true;
                }, default);
            }
            Assert.True(first.EmployeeNumber > 0);
            Assert.NotEqual(first.EmployeeNumber, second.EmployeeNumber);
            await using (var db = new OptiCoreDbContext(options))
            {
                var repository = new EmployeeRepository(db);
                await Assert.ThrowsAsync<DuplicateUsernameException>(() => repository.ExecuteExclusiveAsync(async () =>
                {
                    await repository.AddAsync(new Employee("Test", "Duplicate", " MANAGER-ONE ", hasher.Hash("Test-fixture-password"), "0500000000", "333333333", false, null), default);
                    await repository.SaveChangesAsync(default);
                    return true;
                }, default));
            }
            await using (var db = new OptiCoreDbContext(options))
            {
                var repository = new EmployeeRepository(db);
                await Assert.ThrowsAsync<DuplicateEmployeeNationalIdException>(() => repository.ExecuteExclusiveAsync(async () =>
                {
                    await repository.AddAsync(new Employee("Test", "Duplicate", "unique-name", hasher.Hash("Test-fixture-password"), "0500000000", "111111111", false, null), default);
                    await repository.SaveChangesAsync(default);
                    return true;
                }, default));
            }

            // Hold the actual repository lock while a second connection attempts a service mutation.
            await using var firstDb = new OptiCoreDbContext(options);
            await using var secondDb = new OptiCoreDbContext(options);
            var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var firstRepository = new EmployeeRepository(firstDb);
            var firstRemoval = firstRepository.ExecuteExclusiveAsync(async () =>
            {
                Assert.Equal(2, await firstRepository.CountActiveManagersAsync(timeout.Token));
                var employee = (await firstRepository.GetByIdAsync(first.Id, true, timeout.Token))!;
                employee.Deactivate(first.Id);
                await firstRepository.SaveChangesAsync(timeout.Token);
                locked.SetResult();
                await release.Task.WaitAsync(timeout.Token);
                return true;
            }, timeout.Token);
            await locked.Task.WaitAsync(timeout.Token);
            var secondService = new EmployeeService(new EmployeeRepository(secondDb), hasher);
            var secondRemoval = secondService.SetManagerStatusAsync(second.EmployeeNumber, false, second.Id, timeout.Token);
            try
            {
                Assert.NotSame(secondRemoval, await Task.WhenAny(secondRemoval, Task.Delay(200, timeout.Token)));
            }
            finally { release.TrySetResult(); }
            await firstRemoval;
            await Assert.ThrowsAsync<LastActiveManagerException>(() => secondRemoval);
            await using var verification = new OptiCoreDbContext(options);
            Assert.Equal(1, await verification.Employees.CountAsync(employee => employee.IsActive && employee.IsManager));
            Assert.Equal(2, await verification.Employees.CountAsync());
        }
        finally
        {
            // Only the exact schema created by this test is removed. Public Customer/Employee data is untouched.
            await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin).ExecuteNonQueryAsync();
        }
    }
}
