using Microsoft.Extensions.DependencyInjection;
using OptiCore.Application.Customers;

namespace OptiCore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<Employees.IEmployeeService, Employees.EmployeeService>();
        services.AddScoped<Permissions.IPermissionService, Permissions.PermissionService>();
        return services;
    }
}
