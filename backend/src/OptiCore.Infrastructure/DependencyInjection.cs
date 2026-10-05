using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OptiCore.Infrastructure.Persistence;

namespace OptiCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("OptiCoreDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'OptiCoreDatabase' was not found.");

        services.AddDbContext<OptiCoreDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}