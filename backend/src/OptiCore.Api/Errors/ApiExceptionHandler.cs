using Microsoft.AspNetCore.Diagnostics;
using OptiCore.Application.Products;
using OptiCore.Application.Customers;
using OptiCore.Application.Employees;

namespace OptiCore.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            ProductNotFoundException => (404, "Product was not found."),
            BrandNotFoundException => (404, "Brand was not found."),
            DuplicateBarcodeException => (409, "Product barcode already exists."),
            DuplicateBrandNameException => (409, "Brand name already exists."),
            InactiveBrandException => (409, "Brand is inactive."),
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
