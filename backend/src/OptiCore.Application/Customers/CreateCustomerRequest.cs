namespace OptiCore.Application.Customers;

public sealed record CreateCustomerRequest
{
    public required string NationalId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required string MobilePhone { get; init; }
    public string? HomePhone { get; init; }
    public string? Email { get; init; }
    public required string City { get; init; }
    public string? Street { get; init; }
    public required string Gender { get; init; }
    public string? Notes { get; init; }
    public bool WhatsAppConsent { get; init; }
}
