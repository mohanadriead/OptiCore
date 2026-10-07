using OptiCore.Api.Security;
using OptiCore.Application.Employees;

namespace OptiCore.Api.Endpoints.Employees;

public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/employees").RequireAuthorization("Manager");
        group.MapPost("", async (CreateEmployeeRequest request, HttpContext context, IEmployeeService service, CancellationToken cancellationToken) =>
        {
            var employee = await service.CreateAsync(request, CurrentEmployee.Read(context), cancellationToken);
            return Results.Created($"/api/employees/{employee.EmployeeNumber}", employee);
        });
        group.MapGet("", async (HttpContext context, IEmployeeService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(CurrentEmployee.Read(context), cancellationToken)));
        group.MapGet("/{employeeNumber:int}", async (int employeeNumber, HttpContext context, IEmployeeService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(employeeNumber, CurrentEmployee.Read(context), cancellationToken)));
        group.MapPatch("/{employeeNumber:int}/deactivate", async (int employeeNumber, HttpContext context,
            IEmployeeService service, CancellationToken cancellationToken) =>
        {
            await service.DeactivateAsync(employeeNumber, CurrentEmployee.Read(context), cancellationToken);
            return Results.NoContent();
        });
        group.MapPatch("/{employeeNumber:int}/manager-status", async (int employeeNumber, SetManagerStatusRequest request,
            HttpContext context, IEmployeeService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SetManagerStatusAsync(employeeNumber, request.IsManager, CurrentEmployee.Read(context), cancellationToken)));
        group.MapPost("/{employeeNumber:int}/reset-password", async (int employeeNumber, ResetEmployeePasswordRequest request,
            HttpContext context, IEmployeeService service, CancellationToken cancellationToken) =>
        {
            await service.ResetPasswordAsync(employeeNumber, request.NewPassword, CurrentEmployee.Read(context), cancellationToken);
            return Results.NoContent();
        });
        return app;
    }
}
