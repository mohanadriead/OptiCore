using OptiCore.Domain.Customers;

namespace OptiCore.Application.Customers;

public sealed record CustomerDto(
    Guid Id, int CustomerNumber, string NationalId, string FirstName, string LastName,
    DateOnly DateOfBirth, string MobilePhone, string? HomePhone, string? Email,
    string City, string? Street, string Gender, string? Notes, bool WhatsAppConsent,
    bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc)
{
    internal static CustomerDto FromCustomer(Customer customer) => new(
        customer.Id, customer.CustomerNumber, customer.NationalId, customer.FirstName,
        customer.LastName, customer.DateOfBirth, customer.MobilePhone, customer.HomePhone,
        customer.Email, customer.City, customer.Street, customer.Gender, customer.Notes,
        customer.WhatsAppConsent, customer.IsActive, customer.CreatedAtUtc, customer.UpdatedAtUtc);
}
