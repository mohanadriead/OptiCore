using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OptiCore.Infrastructure.Persistence;

// Offline model/migration generation: never start the API, bootstrap, read secrets,
// or connect to a database. Applying migrations requires a separately supplied connection.
public sealed class OptiCoreDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OptiCoreDbContext>
{
    public OptiCoreDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OptiCoreDbContext>().UseNpgsql().Options);
}
