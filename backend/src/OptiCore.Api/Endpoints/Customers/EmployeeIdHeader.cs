namespace OptiCore.Api.Endpoints.Customers;

internal static class EmployeeIdHeader
{
    public static Guid Read(HttpContext context)
    {
        // Temporary development actor attribution only, not authentication or authorization.
        // Replace this header with the authenticated employee identity once authentication exists.
        var values = context.Request.Headers["X-Employee-Id"];
        if (values.Count != 1 ||
    !Guid.TryParse(values[0], out var employeeId) ||
    employeeId == Guid.Empty)
        {
            throw new ArgumentException(
                "X-Employee-Id must contain a single non-empty valid Guid.");
        }
        return employeeId;
    }
}
