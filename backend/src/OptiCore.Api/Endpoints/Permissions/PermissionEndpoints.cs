using OptiCore.Api.Security;
using OptiCore.Application.Permissions;

namespace OptiCore.Api.Endpoints.Permissions;

public static class PermissionEndpoints
{
    public static IEndpointRouteBuilder MapPermissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/permissions", async (HttpContext context, IPermissionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCatalogAsync(CurrentEmployee.Read(context), cancellationToken)))
            .RequireAuthorization("Manager");
        var employee = app.MapGroup("/api/employees/{employeeNumber:int}/permissions").RequireAuthorization("Manager");
        employee.MapGet("", async (int employeeNumber, HttpContext context, IPermissionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAssignedPermissionsAsync(employeeNumber, CurrentEmployee.Read(context), cancellationToken)));
        employee.MapPut("", async (int employeeNumber, SetPermissionsRequest request, HttpContext context,
            IPermissionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SetAssignedPermissionsAsync(employeeNumber, request.Permissions, CurrentEmployee.Read(context), cancellationToken)));
        app.MapGet("/api/auth/permissions", async (HttpContext context, IPermissionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetEffectivePermissionsAsync(CurrentEmployee.Read(context), cancellationToken)))
            .RequireAuthorization("Employee");
        return app;
    }
}
