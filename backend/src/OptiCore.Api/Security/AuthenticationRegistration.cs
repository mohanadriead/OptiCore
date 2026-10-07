using Microsoft.AspNetCore.Authentication.Cookies;

namespace OptiCore.Api.Security;

public static class AuthenticationRegistration
{
    public static IServiceCollection AddEmployeeAuthentication(this IServiceCollection services, bool development)
    {
        services.AddScoped<EmployeeCookieEvents>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = development ? "OptiCore.Auth" : "__Host-OptiCore.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.Path = "/";
            // Absolute ticket lifetime, not an inactivity timer. Browser cookie is non-persistent.
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = false;
            options.EventsType = typeof(EmployeeCookieEvents);
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Employee", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("Manager", policy => policy.RequireAuthenticatedUser().RequireRole("Manager"));
        });
        return services;
    }
}
