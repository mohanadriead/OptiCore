using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OptiCore.Api.Security;
using OptiCore.Application.Employees;

namespace OptiCore.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");
        group.MapPost("/login", async (LoginRequest request, IEmployeeService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var employee = await service.AuthenticateAsync(request, cancellationToken);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CurrentEmployee.Principal(employee),
                new AuthenticationProperties { IsPersistent = false, AllowRefresh = false });
            return Results.Ok(employee);
        }).AllowAnonymous();
        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization("Employee");
        group.MapGet("/me", async (HttpContext context, IEmployeeService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentAsync(CurrentEmployee.Read(context), cancellationToken)))
            .RequireAuthorization("Employee");
        group.MapPost("/change-password", async (ChangeOwnPasswordRequest request, HttpContext context,
            IEmployeeService service, CancellationToken cancellationToken) =>
        {
            await service.ChangeOwnPasswordAsync(request, CurrentEmployee.Read(context), cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization("Employee");
        return app;
    }
}
