using Microsoft.EntityFrameworkCore;

namespace OptiCore.Infrastructure.Persistence;

public class OptiCoreDbContext : DbContext
{
    public OptiCoreDbContext(DbContextOptions<OptiCoreDbContext> options)
        : base(options)
    {
    }
}