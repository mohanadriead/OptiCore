using Microsoft.Extensions.Configuration;
using OptiCore.Application.Employees;
using OptiCore.Domain.Employees;
using OptiCore.Infrastructure.Security;

namespace OptiCore.Tests.Employees;

public sealed class EmployeeTests
{
    private readonly FakeEmployeeRepository repository = new();
    private readonly EmployeePasswordHasher hasher = new();
    private EmployeeService Service => new(repository, hasher);
    private static CreateEmployeeRequest Request => new()
    {
        FirstName = " New ", LastName = " Employee ", Username = " NewUser ", Password = "Unit-test-password",
        Phone = " 0501234567 ", NationalId = " 987654321 ", IsManager = false
    };

    [Fact]
    public async Task Create_NormalizesHashesGeneratesNumberAndAudits()
    {
        var manager = repository.Seed();
        var result = await Service.CreateAsync(Request, manager.Id, default);
        var employee = repository.Employees.Last();
        Assert.Equal("New", employee.FirstName);
        Assert.Equal("Employee", employee.LastName);
        Assert.Equal("NewUser", employee.Username);
        Assert.Equal("NEWUSER", employee.NormalizedUsername);
        Assert.Equal("987654321", employee.NationalId);
        Assert.Equal("0501234567", employee.Phone);
        Assert.True(employee.IsActive);
        Assert.False(employee.IsManager);
        Assert.NotEqual(Guid.Empty, employee.Id);
        Assert.Equal(manager.Id, employee.CreatedByEmployeeId);
        Assert.NotEqual(default, employee.CreatedAtUtc);
        Assert.Null(employee.UpdatedAtUtc);
        Assert.Equal(2, result.EmployeeNumber);
        Assert.NotEqual(Request.Password, employee.PasswordHash);
        Assert.True(hasher.Verify(employee.PasswordHash, Request.Password));
        Assert.False(hasher.Verify(employee.PasswordHash, "wrong-password"));
        Assert.DoesNotContain("password", System.Text.Json.JsonSerializer.Serialize(result).ToLowerInvariant());
    }

    [Theory]
    [InlineData("mohanad")]
    [InlineData(" MOHANAD ")]
    [InlineData("Mohanad")]
    public void UsernameNormalization(string name) => Assert.Equal("MOHANAD", Employee.NormalizeUsername(name));

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("        ")]
    public void PasswordBaselineRejectsInvalid(string password) => Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(password));

    [Fact]
    public void PasswordLimitsAndNoCompositionRules()
    {
        PasswordPolicy.Validate("abcdefgh");
        PasswordPolicy.Validate(new string('x', 128));
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(new string('x', 129)));
        Assert.NotEqual(hasher.Hash("abcdefgh"), hasher.Hash("abcdefgh"));
    }

    [Fact]
    public void DomainRejectsMissingAndOversizedFields()
    {
        Assert.Throws<ArgumentException>(() => new Employee(" ", "Last", "user", "hash", "phone", "123", false, null));
        Assert.Throws<ArgumentException>(() => new Employee("First", "Last", "user", "hash", "phone", new string('1', 10), false, null));
    }

    [Fact]
    public async Task DuplicateUsernameIsCaseInsensitiveIncludingInactive()
    {
        var manager = repository.Seed();
        repository.Seed("NewUser", false, "111111111").Deactivate(manager.Id);
        await Assert.ThrowsAsync<DuplicateUsernameException>(() => Service.CreateAsync(Request with { Username = "newuser" }, manager.Id, default));
    }

    [Fact]
    public async Task DuplicateNationalIdRejected()
    {
        var manager = repository.Seed();
        await Assert.ThrowsAsync<DuplicateEmployeeNationalIdException>(() => Service.CreateAsync(Request with { NationalId = " 123456789 " }, manager.Id, default));
    }

    [Fact]
    public async Task PasswordChangeVerifiesCurrentAndRecordsActor()
    {
        var employee = repository.Seed();
        var original = employee.PasswordHash;
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => Service.ChangeOwnPasswordAsync(new() { CurrentPassword = "wrong", NewPassword = "new-password" }, employee.Id, default));
        Assert.Equal(original, employee.PasswordHash);
        await Service.ChangeOwnPasswordAsync(new() { CurrentPassword = "Test-password-123", NewPassword = "new-password" }, employee.Id, default);
        Assert.True(hasher.Verify(employee.PasswordHash, "new-password"));
        Assert.False(hasher.Verify(employee.PasswordHash, "Test-password-123"));
        Assert.Equal(employee.Id, employee.UpdatedByEmployeeId);
    }

    [Fact]
    public async Task ManagerResetsPassword()
    {
        var manager = repository.Seed();
        var employee = repository.Seed("employee", false, "222222222");
        await Service.ResetPasswordAsync(employee.EmployeeNumber, "new-password", manager.Id, default);
        Assert.True(hasher.Verify(employee.PasswordHash, "new-password"));
        Assert.Equal(manager.Id, employee.UpdatedByEmployeeId);
    }

    [Fact]
    public async Task NonManagerCannotManageEmployees()
    {
        var employee = repository.Seed(manager: false);
        await Assert.ThrowsAsync<ManagerRequiredException>(() => Service.CreateAsync(Request, employee.Id, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => Service.ResetPasswordAsync(1, "new-password", employee.Id, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => Service.DeactivateAsync(1, employee.Id, default));
        await Assert.ThrowsAsync<ManagerRequiredException>(() => Service.SetManagerStatusAsync(1, true, employee.Id, default));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LastManagerCannotBeRemoved(bool deactivate)
    {
        var manager = repository.Seed();
        await Assert.ThrowsAsync<LastActiveManagerException>(() => deactivate
            ? Service.DeactivateAsync(1, manager.Id, default)
            : Service.SetManagerStatusAsync(1, false, manager.Id, default));
        Assert.True(manager.IsActive && manager.IsManager);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OneOfMultipleManagersCanBeRemoved(bool deactivate)
    {
        var manager = repository.Seed();
        var other = repository.Seed("other", true, "222222222");
        if (deactivate) await Service.DeactivateAsync(other.EmployeeNumber, manager.Id, default);
        else await Service.SetManagerStatusAsync(other.EmployeeNumber, false, manager.Id, default);
        Assert.False(other.IsActive && other.IsManager);
        Assert.Equal(manager.Id, other.UpdatedByEmployeeId);
        Assert.Equal(2, repository.Employees.Count);
    }

    [Fact]
    public async Task PromotionAndIdempotentDeactivation()
    {
        var manager = repository.Seed();
        var other = repository.Seed("other", false, "222222222");
        await Service.SetManagerStatusAsync(2, true, manager.Id, default);
        Assert.True(other.IsManager);
        await Service.DeactivateAsync(2, manager.Id, default);
        var updated = other.UpdatedAtUtc;
        await Service.DeactivateAsync(2, manager.Id, default);
        Assert.Equal(updated, other.UpdatedAtUtc);
    }

    [Fact]
    public async Task LoginChecksPasswordBeforeInactiveStatus()
    {
        var employee = repository.Seed();
        var result = await Service.AuthenticateAsync(new() { Username = " MANAGER ", Password = "Test-password-123" }, default);
        Assert.Equal(employee.Id, result.Id);
        employee.Deactivate(employee.Id);
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => Service.AuthenticateAsync(new() { Username = "manager", Password = "wrong" }, default));
        await Assert.ThrowsAsync<InactiveEmployeeException>(() => Service.AuthenticateAsync(new() { Username = "manager", Password = "Test-password-123" }, default));
    }

    [Fact]
    public async Task BootstrapFailsClosedThenCreatesExactlyOnce()
    {
        var missing = new ConfigurationBuilder().Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BootstrapManager(repository, hasher, missing).InitializeAsync(default));
        Assert.Empty(repository.Employees);
        // Isolated test fixture data only; never used as installation credentials.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapManager:FirstName"] = "Test", ["BootstrapManager:LastName"] = "Fixture",
            ["BootstrapManager:Username"] = "fixture", ["BootstrapManager:Password"] = "Fixture-password",
            ["BootstrapManager:Phone"] = "0500000000", ["BootstrapManager:NationalId"] = "111111111"
        }).Build();
        await new BootstrapManager(repository, hasher, config).InitializeAsync(default);
        var employee = Assert.Single(repository.Employees);
        Assert.True(employee.IsManager && employee.IsActive);
        Assert.Null(employee.CreatedByEmployeeId);
        Assert.True(hasher.Verify(employee.PasswordHash, "Fixture-password"));
        await new BootstrapManager(repository, hasher, missing).InitializeAsync(default);
        Assert.Same(employee, Assert.Single(repository.Employees));
    }
}
