using Microsoft.Extensions.DependencyInjection;
using OptiCore.Application.Customers;

namespace OptiCore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Attendance.AttendanceMidnightPolicy>();
        services.AddScoped<Attendance.IAttendanceService, Attendance.AttendanceService>();
        services.AddScoped<Employees.IEmployeeService, Employees.EmployeeService>();
        services.AddScoped<Permissions.IPermissionService, Permissions.PermissionService>();
        return services;
    }
}
