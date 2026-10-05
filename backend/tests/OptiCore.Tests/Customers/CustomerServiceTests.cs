using OptiCore.Application.Customers;
using OptiCore.Domain.Customers;

namespace OptiCore.Tests.Customers;

public class CustomerServiceTests
{
    private readonly FakeCustomerRepository repository = new();
    private CustomerService Service => new(repository);
    private static readonly Guid Actor = Guid.CreateVersion7();

    [Fact]
    public async Task Create_NormalizesValuesAndReturnsNumberAssignedOnSave()
    {
        using var cancellation = new CancellationTokenSource();
        var request = ValidRequest() with { NationalId = " 123456789 ", FirstName = " Ahmad ", Email = " " };

        var result = await Service.CreateCustomerAsync(request, Actor, cancellation.Token);

        var customer = Assert.Single(repository.Customers);
        Assert.Equal(0, repository.NumberBeforeSave);
        Assert.Equal(42, result.CustomerNumber);
        Assert.Equal(customer.Id, result.Id);
        Assert.Equal("123456789", result.NationalId);
        Assert.Equal("Ahmad", result.FirstName);
        Assert.Null(result.Email);
        Assert.True(result.IsActive);
        Assert.False(result.WhatsAppConsent);
        Assert.Equal(Actor, customer.CreatedByEmployeeId);
        Assert.NotEqual(default, result.CreatedAtUtc);
        Assert.Null(result.UpdatedAtUtc);
        Assert.Equal(1, repository.SaveCount);
        Assert.All(repository.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task Create_RejectsTrimmedDuplicateEvenWhenExistingCustomerIsInactive()
    {
        var existing = Seed();
        existing.Deactivate(Actor);

        await Assert.ThrowsAsync<DuplicateNationalIdException>(() =>
            Service.CreateCustomerAsync(ValidRequest() with { NationalId = " 123456789 " }, Actor, default));

        Assert.Single(repository.Customers);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Create_UsesDomainRequiredFieldValidation()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateCustomerAsync(ValidRequest() with { FirstName = " " }, Actor, default));
        Assert.Empty(repository.Customers);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Create_RejectsValuesExceedingExistingStorageLimits()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateCustomerAsync(ValidRequest() with { Notes = new string('x', 4001) }, Actor, default));
        Assert.Empty(repository.Customers);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task GetByCustomerNumber_ReturnsExistingCustomerWithoutRequestingTracking()
    {
        var existing = Seed();
        var result = await Service.GetByCustomerNumberAsync(42, default);
        Assert.Equal(existing.Id, result.Id);
        Assert.False(repository.LastForUpdate);
    }

    [Fact]
    public async Task GetByCustomerNumber_ThrowsWhenMissing()
    {
        await Assert.ThrowsAsync<CustomerNotFoundException>(() => Service.GetByCustomerNumberAsync(42, default));
    }

    [Fact]
    public async Task GetByNationalId_TrimsAndReturnsInactiveCustomer()
    {
        var customer = Seed();
        customer.Deactivate(Actor);
        var result = await Service.GetByNationalIdAsync(" 123456789 ", default);
        Assert.Equal(customer.Id, result.Id);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task GetByNationalId_ThrowsWhenMissing()
    {
        await Assert.ThrowsAsync<CustomerNotFoundException>(() => Service.GetByNationalIdAsync("123456789", default));
    }

    [Fact]
    public async Task Update_UsesDomainNormalizationAndRecordsUpdaterWithoutChangingIdentityOrConsent()
    {
        var customer = Seed();
        var originalId = customer.Id;
        var createdAt = customer.CreatedAtUtc;
        var updater = Guid.CreateVersion7();

        var result = await Service.UpdateCustomerAsync(42, UpdateRequest(), updater, default);

        Assert.Equal("New", result.FirstName);
        Assert.Equal("Name", result.LastName);
        Assert.Equal("0529999999", result.MobilePhone);
        Assert.Equal("Nazareth", result.City);
        Assert.Null(result.HomePhone);
        Assert.Equal("new@example.com", result.Email);
        Assert.Equal(updater, customer.UpdatedByEmployeeId);
        Assert.NotNull(result.UpdatedAtUtc);
        Assert.Equal(createdAt, result.CreatedAtUtc);
        Assert.Equal(originalId, result.Id);
        Assert.Equal(42, result.CustomerNumber);
        Assert.Equal("123456789", result.NationalId);
        Assert.False(result.WhatsAppConsent);
        Assert.True(result.IsActive);
        Assert.True(repository.LastForUpdate);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task InvalidUpdate_DoesNotMutateOrSaveCustomer()
    {
        var customer = Seed();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.UpdateCustomerAsync(42, UpdateRequest() with { LastName = " " }, Actor, default));
        Assert.Equal("Ahmad", customer.FirstName);
        Assert.Null(customer.UpdatedAtUtc);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task OversizedUpdate_DoesNotMutateOrSaveCustomer()
    {
        var customer = Seed();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.UpdateCustomerAsync(42, UpdateRequest() with { Email = new string('x', 255) }, Actor, default));
        Assert.Equal("Ahmad", customer.FirstName);
        Assert.Null(customer.UpdatedAtUtc);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ConsentChange_RecordsConsentAndUpdater()
    {
        var customer = Seed();
        var updater = Guid.CreateVersion7();
        var result = await Service.SetWhatsAppConsentAsync(42, true, updater, default);
        Assert.True(result.WhatsAppConsent);
        Assert.Equal(updater, customer.UpdatedByEmployeeId);
        Assert.NotNull(result.UpdatedAtUtc);
        Assert.True(repository.LastForUpdate);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Deactivate_PreservesCustomerAndRecordsUpdater()
    {
        var customer = Seed();
        var updater = Guid.CreateVersion7();
        await Service.DeactivateCustomerAsync(42, updater, default);
        Assert.Same(customer, Assert.Single(repository.Customers));
        Assert.False(customer.IsActive);
        Assert.Equal(updater, customer.UpdatedByEmployeeId);
        Assert.NotNull(customer.UpdatedAtUtc);
        Assert.True(repository.LastForUpdate);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task RepeatedDeactivation_IsIdempotent()
    {
        var customer = Seed();
        await Service.DeactivateCustomerAsync(42, Actor, default);
        var updatedAt = customer.UpdatedAtUtc;
        await Service.DeactivateCustomerAsync(42, Guid.CreateVersion7(), default);
        Assert.Equal(updatedAt, customer.UpdatedAtUtc);
        Assert.Equal(Actor, customer.UpdatedByEmployeeId);
        Assert.Equal(1, repository.SaveCount);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("consent")]
    [InlineData("deactivate")]
    public async Task Mutations_ThrowWhenCustomerIsMissing(string operation)
    {
        await Assert.ThrowsAsync<CustomerNotFoundException>(async () =>
        {
            if (operation == "update")
                await Service.UpdateCustomerAsync(42, UpdateRequest(), Actor, default);
            else if (operation == "consent")
                await Service.SetWhatsAppConsentAsync(42, true, Actor, default);
            else
                await Service.DeactivateCustomerAsync(42, Actor, default);
        });
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Search_ReturnsAllSharedPhoneMatchesIncludingInactiveCustomer()
    {
        var first = Seed();
        var second = Seed("987654321", 43);
        second.Deactivate(Actor);
        var results = await Service.SearchAsync(" 0501234567 ", default);
        Assert.Equal("0501234567", repository.LastQuery);
        Assert.Equal(new[] { first.Id, second.Id }, results.Select(customer => customer.Id));
        Assert.False(results[1].IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_RejectsEmptyQueryWithoutCallingRepository(string? query)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Service.SearchAsync(query, default));
        Assert.Null(repository.LastQuery);
    }

    private Customer Seed(string nationalId = "123456789", int number = 42)
    {
        var customer = new Customer(nationalId, "Ahmad", "Ali", new DateOnly(1995, 4, 20),
            "0501234567", "Haifa", "Male", Actor);
        FakeCustomerRepository.AssignNumber(customer, number);
        repository.Customers.Add(customer);
        return customer;
    }

    private static CreateCustomerRequest ValidRequest() => new()
    {
        NationalId = "123456789", FirstName = "Ahmad", LastName = "Ali",
        DateOfBirth = new DateOnly(1995, 4, 20), MobilePhone = "0501234567",
        City = "Haifa", Gender = "Male"
    };

    private static UpdateCustomerRequest UpdateRequest() => new()
    {
        FirstName = " New ", LastName = " Name ", DateOfBirth = new DateOnly(1990, 2, 3),
        MobilePhone = " 0529999999 ", HomePhone = " ", Email = " new@example.com ",
        City = "Nazareth", Gender = "Male"
    };

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public int SaveCount { get; private set; }
        public int NumberBeforeSave { get; private set; }
        public bool LastForUpdate { get; private set; }
        public string? LastQuery { get; private set; }

        public Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(Customers.Any(customer => customer.NationalId == nationalId));
        }

        public Task AddAsync(Customer customer, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            NumberBeforeSave = customer.CustomerNumber;
            Customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<Customer?> GetByCustomerNumberAsync(int customerNumber, bool forUpdate, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            LastForUpdate = forUpdate;
            return Task.FromResult(Customers.SingleOrDefault(customer => customer.CustomerNumber == customerNumber));
        }

        public Task<Customer?> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(Customers.SingleOrDefault(customer => customer.NationalId == nationalId));
        }

        public Task<IReadOnlyList<Customer>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            LastQuery = query;
            // This fake exercises service result handling; PostgreSQL owns production search semantics.
            return Task.FromResult<IReadOnlyList<Customer>>(Customers
                .Where(customer => customer.MobilePhone.Contains(query) || customer.HomePhone?.Contains(query) == true)
                .ToArray());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            SaveCount++;
            foreach (var customer in Customers.Where(customer => customer.CustomerNumber == 0))
                AssignNumber(customer, 42);
            return Task.CompletedTask;
        }

        // Only the test fake simulates the number that PostgreSQL assigns during SaveChanges.
        public static void AssignNumber(Customer customer, int number) =>
            typeof(Customer).GetProperty(nameof(Customer.CustomerNumber))!.SetValue(customer, number);
    }
}
