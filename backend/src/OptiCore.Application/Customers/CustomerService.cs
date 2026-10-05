using OptiCore.Domain.Customers;

namespace OptiCore.Application.Customers;

public sealed class CustomerService(ICustomerRepository repository) : ICustomerService
{
    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, Guid employeeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NationalId);
        CheckLength(request.NationalId, 9, nameof(request.NationalId));
        if (await repository.NationalIdExistsAsync(request.NationalId.Trim(), cancellationToken))
            throw new DuplicateNationalIdException();

        CheckLengths(request.FirstName, request.LastName, request.MobilePhone, request.HomePhone,
            request.Email, request.City, request.Street, request.Gender, request.Notes);
        var customer = new Customer(request.NationalId, request.FirstName, request.LastName,
            request.DateOfBirth, request.MobilePhone, request.City, request.Gender, employeeId,
            request.HomePhone, request.Email, request.Street, request.Notes, request.WhatsAppConsent);
        await repository.AddAsync(customer, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return CustomerDto.FromCustomer(customer);
    }

    public async Task<CustomerDto> GetByCustomerNumberAsync(int customerNumber, CancellationToken cancellationToken) =>
        CustomerDto.FromCustomer(await FindAsync(customerNumber, false, cancellationToken));

    public async Task<CustomerDto> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nationalId);
        var customer = await repository.GetByNationalIdAsync(nationalId.Trim(), cancellationToken)
            ?? throw new CustomerNotFoundException();
        return CustomerDto.FromCustomer(customer);
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var customers = await repository.SearchAsync(query.Trim(), cancellationToken);
        return customers.Select(CustomerDto.FromCustomer).ToArray();
    }

    public async Task<CustomerDto> UpdateCustomerAsync(int customerNumber, UpdateCustomerRequest request,
        Guid employeeId, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(customerNumber, true, cancellationToken);
        CheckLengths(request.FirstName, request.LastName, request.MobilePhone, request.HomePhone,
            request.Email, request.City, request.Street, request.Gender, request.Notes);
        customer.UpdateDetails(request.FirstName, request.LastName, request.DateOfBirth,
            request.MobilePhone, request.City, request.Gender, employeeId,
            request.HomePhone, request.Email, request.Street, request.Notes);
        await repository.SaveChangesAsync(cancellationToken);
        return CustomerDto.FromCustomer(customer);
    }

    public async Task<CustomerDto> SetWhatsAppConsentAsync(int customerNumber, bool consent,
        Guid employeeId, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(customerNumber, true, cancellationToken);
        customer.SetWhatsAppConsent(consent, employeeId);
        await repository.SaveChangesAsync(cancellationToken);
        return CustomerDto.FromCustomer(customer);
    }

    public async Task DeactivateCustomerAsync(int customerNumber, Guid employeeId, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(customerNumber, true, cancellationToken);
        if (!customer.IsActive)
            return;
        customer.Deactivate(employeeId);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Customer> FindAsync(int customerNumber, bool forUpdate, CancellationToken cancellationToken) =>
        await repository.GetByCustomerNumberAsync(customerNumber, forUpdate, cancellationToken)
            ?? throw new CustomerNotFoundException();

    // Match the existing storage limits before mutating an entity or saving to PostgreSQL.
    private static void CheckLengths(string firstName, string lastName, string mobilePhone,
        string? homePhone, string? email, string city, string? street, string gender, string? notes)
    {
        CheckLength(firstName, 100, nameof(firstName));
        CheckLength(lastName, 100, nameof(lastName));
        CheckLength(mobilePhone, 30, nameof(mobilePhone));
        CheckLength(homePhone, 30, nameof(homePhone));
        CheckLength(email, 254, nameof(email));
        CheckLength(city, 100, nameof(city));
        CheckLength(street, 200, nameof(street));
        CheckLength(gender, 50, nameof(gender));
        CheckLength(notes, 4000, nameof(notes));
    }

    private static void CheckLength(string? value, int maximum, string field)
    {
        if (value?.Trim().Length > maximum)
            throw new ArgumentException(field + " must be at most " + maximum + " characters.");
    }
}
