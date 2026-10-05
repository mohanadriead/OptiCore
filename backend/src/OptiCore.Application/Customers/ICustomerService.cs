namespace OptiCore.Application.Customers;

public interface ICustomerService
{
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, Guid employeeId, CancellationToken cancellationToken);
    Task<CustomerDto> GetByCustomerNumberAsync(int customerNumber, CancellationToken cancellationToken);
    Task<CustomerDto> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerDto>> SearchAsync(string? query, CancellationToken cancellationToken);
    Task<CustomerDto> UpdateCustomerAsync(int customerNumber, UpdateCustomerRequest request, Guid employeeId, CancellationToken cancellationToken);
    Task<CustomerDto> SetWhatsAppConsentAsync(int customerNumber, bool consent, Guid employeeId, CancellationToken cancellationToken);
    Task DeactivateCustomerAsync(int customerNumber, Guid employeeId, CancellationToken cancellationToken);
}
