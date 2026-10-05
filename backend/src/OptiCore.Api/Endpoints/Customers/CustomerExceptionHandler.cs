using Microsoft.AspNetCore.Diagnostics;
using OptiCore.Application.Customers;

namespace OptiCore.Api.Endpoints.Customers;

public sealed class CustomerExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            CustomerNotFoundException => (StatusCodes.Status404NotFound, "Customer was not found."),
            DuplicateNationalIdException => (StatusCodes.Status409Conflict, "A customer with this NationalId already exists."),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request body or parameters."),
            _ => (0, "")
        };
        if (status == 0)
            return false;

        await Results.Problem(statusCode: status, title: message).ExecuteAsync(context);
        return true;
    }
}
