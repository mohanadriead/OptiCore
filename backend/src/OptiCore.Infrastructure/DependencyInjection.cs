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

        services.AddScoped<OptiCore.Application.Customers.ICustomerRepository,
            OptiCore.Infrastructure.Persistence.Repositories.CustomerRepository>();
        services.AddScoped<OptiCore.Application.Employees.IEmployeeRepository,
            OptiCore.Infrastructure.Persistence.Repositories.EmployeeRepository>();
        services.AddSingleton<OptiCore.Application.Employees.IPasswordHasher, Security.EmployeePasswordHasher>();
        services.AddScoped<Security.BootstrapManager>();
        services.AddScoped<OptiCore.Application.Permissions.IEmployeePermissionRepository,
            Persistence.Repositories.EmployeePermissionRepository>();

        return services;
    }
}
