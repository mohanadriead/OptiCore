using System.Security.Claims;
using OptiCore.Application.Employees;

namespace OptiCore.Api.Security;

public static class CurrentEmployee
{
    public static Guid Read(HttpContext context) => Read(context.User);

    public static Guid Read(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
            throw new InvalidCredentialsException();
        return id;
    }

    public static ClaimsPrincipal Principal(EmployeeDto employee) => new(new ClaimsIdentity(
        new[]
        {
            new Claim(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new Claim(ClaimTypes.Name, employee.Username),
            new Claim("EmployeeNumber", employee.EmployeeNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
        }.Concat(employee.IsManager ? [new Claim(ClaimTypes.Role, "Manager")] : []),
        Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme));
}
