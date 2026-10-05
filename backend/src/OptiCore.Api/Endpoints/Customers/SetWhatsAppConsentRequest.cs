namespace OptiCore.Api.Endpoints.Customers;

public sealed record SetWhatsAppConsentRequest
{
    public required bool Consent { get; init; }
}
