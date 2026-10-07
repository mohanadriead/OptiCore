using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OptiCore.Application.Employees;

namespace OptiCore.Api.Security;

public sealed class EmployeeCookieEvents(IEmployeeService employees) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        try
        {
            var employee = await employees.GetCurrentAsync(CurrentEmployee.Read(context.Principal), context.HttpContext.RequestAborted);
            context.ReplacePrincipal(CurrentEmployee.Principal(employee));
        }
        catch (Exception exception) when (exception is InvalidCredentialsException or InactiveEmployeeException)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
