using Microsoft.AspNetCore.Diagnostics;
using OptiCore.Application.Attendance;
using OptiCore.Application.Customers;
using OptiCore.Application.Employees;

namespace OptiCore.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            DuplicateAttendanceException => (409, "לעובד כבר קיימת כניסה פתוחה."),
            NoOpenAttendanceException => (409, "לא קיימת כניסה פתוחה לעובד."),
            InactiveAttendanceEmployeeException => (403, "לא ניתן לרשום כניסה לעובד לא פעיל."),
            CustomerNotFoundException => (StatusCodes.Status404NotFound, "Customer was not found."),
            DuplicateNationalIdException => (StatusCodes.Status409Conflict, "A customer with this NationalId already exists."),
            EmployeeNotFoundException => (404, "Employee was not found."),
            InvalidCredentialsException => (401, "Invalid username or password."),
            InactiveEmployeeException => (403, "Employee is inactive."),
            ManagerRequiredException => (403, "Manager authorization is required."),
            DuplicateUsernameException => (409, "Username already exists."),
            DuplicateEmployeeNationalIdException => (409, "An employee with this NationalId already exists."),
            LastActiveManagerException => (409, "At least one active manager must remain."),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request body or parameters."),
            _ => (500, "Unable to complete the request.")
        };

        await Results.Problem(statusCode: status, title: message).ExecuteAsync(context);
        return true;
    }
}
