using Microsoft.Extensions.DependencyInjection;
using OptiCore.Application.Customers;

namespace OptiCore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        return services;
    }
}
