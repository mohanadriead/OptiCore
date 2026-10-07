using Microsoft.Extensions.Configuration;
using OptiCore.Application.Employees;
using OptiCore.Domain.Employees;
using OptiCore.Infrastructure.Security;

namespace OptiCore.Tests.Employees;

public sealed class EmployeeValidationTests
{
    [Fact]
    public void EmployeeRequiresAsciiIdAndPhone()
    {
        var valid = new Employee("First", "Last", "user", "test-hash", new string('0', 10), new string('0', 9), false, null);
        Assert.Equal(new string('0', 9), valid.NationalId);
        Assert.Equal(new string('0', 10), valid.Phone);
        foreach (var invalid in new[] { "letters", new string('1', 8), new string('1', 10), new string('١', 9), new string('１', 9) })
            Assert.Throws<ArgumentException>(() => new Employee("First", "Last", "user", "test-hash", new string('0', 10), invalid, false, null));
        foreach (var invalid in new[] { "letters", new string('1', 9), new string('1', 11), new string('١', 10), new string('１', 10) })
            Assert.Throws<ArgumentException>(() => new Employee("First", "Last", "user", "test-hash", invalid, new string('0', 9), false, null));
    }

    [Theory]
    [InlineData("NationalId")]
    [InlineData("Phone")]
    public async Task ManagerCreationAndBootstrapRejectInvalidFields(string field)
    {
        var repository = new FakeEmployeeRepository();
        var manager = repository.Seed();
        var hasher = new EmployeePasswordHasher();
        var request = new CreateEmployeeRequest
        {
            FirstName = "First", LastName = "Last", Username = "new-user", Password = "Test-fixture-password",
            NationalId = field == "NationalId" ? "invalid-id" : new string('0', 9),
            Phone = field == "Phone" ? "invalid-phone" : new string('0', 10), IsManager = false
        };
        await Assert.ThrowsAsync<ArgumentException>(() => new EmployeeService(repository, hasher).CreateAsync(request, manager.Id, default));
        Assert.Single(repository.Employees);

        var empty = new FakeEmployeeRepository();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapManager:FirstName"] = request.FirstName, ["BootstrapManager:LastName"] = request.LastName,
            ["BootstrapManager:Username"] = request.Username, ["BootstrapManager:Password"] = request.Password,
            ["BootstrapManager:NationalId"] = request.NationalId, ["BootstrapManager:Phone"] = request.Phone
        }).Build();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new BootstrapManager(empty, hasher, config).InitializeAsync(default));
        Assert.DoesNotContain("invalid-id", error.Message);
        Assert.DoesNotContain("invalid-phone", error.Message);
        Assert.DoesNotContain(request.Password, error.Message);
        Assert.Null(error.InnerException);
        Assert.Empty(empty.Employees);
    }
}
