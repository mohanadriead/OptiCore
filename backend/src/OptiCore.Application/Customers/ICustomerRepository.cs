using OptiCore.Domain.Customers;

namespace OptiCore.Application.Customers;

public interface ICustomerRepository
{
    Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task<Customer?> GetByCustomerNumberAsync(int customerNumber, bool forUpdate, CancellationToken cancellationToken);
    Task<Customer?> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> SearchAsync(string query, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
