using OptiCore.Application.Customers;

namespace OptiCore.Api.Endpoints.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers");

        group.MapPost("", async (CreateCustomerRequest request, HttpContext context,
            ICustomerService service, CancellationToken cancellationToken) =>
        {
            var customer = await service.CreateCustomerAsync(request, EmployeeIdHeader.Read(context), cancellationToken);
            return Results.Created($"/api/customers/{customer.CustomerNumber}", customer);
        });

        group.MapGet("/{customerNumber:int}", async (int customerNumber,
            ICustomerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetByCustomerNumberAsync(customerNumber, cancellationToken)));

        group.MapGet("/by-national-id/{nationalId}", async (string nationalId,
            ICustomerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetByNationalIdAsync(nationalId, cancellationToken)));

        group.MapGet("/search", async (string? q, ICustomerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SearchAsync(q, cancellationToken)));

        group.MapPut("/{customerNumber:int}", async (int customerNumber, UpdateCustomerRequest request,
            HttpContext context, ICustomerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateCustomerAsync(customerNumber, request, EmployeeIdHeader.Read(context), cancellationToken)));

        group.MapPatch("/{customerNumber:int}/whatsapp-consent", async (int customerNumber,
            SetWhatsAppConsentRequest request, HttpContext context, ICustomerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SetWhatsAppConsentAsync(customerNumber, request.Consent, EmployeeIdHeader.Read(context), cancellationToken)));

        group.MapPatch("/{customerNumber:int}/deactivate", async (int customerNumber,
            HttpContext context, ICustomerService service, CancellationToken cancellationToken) =>
        {
            await service.DeactivateCustomerAsync(customerNumber, EmployeeIdHeader.Read(context), cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
