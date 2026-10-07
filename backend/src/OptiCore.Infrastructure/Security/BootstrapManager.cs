using Microsoft.Extensions.Configuration;
using OptiCore.Application.Employees;
using OptiCore.Domain.Employees;

namespace OptiCore.Infrastructure.Security;

public sealed class BootstrapManager(IEmployeeRepository repository, IPasswordHasher hasher, IConfiguration configuration)
{
    public Task InitializeAsync(CancellationToken cancellationToken) => repository.ExecuteExclusiveAsync(async () =>
    {
        if (await repository.AnyAsync(cancellationToken)) return false;
        Employee employee;
        try
        {
            string Read(string key) => configuration["BootstrapManager:" + key] ?? string.Empty;
            var password = Read("Password");
            PasswordPolicy.Validate(password);
            employee = new Employee(Read("FirstName"), Read("LastName"), Read("Username"), hasher.Hash(password),
                Read("Phone"), Read("NationalId"), true, null);
        }
        catch (ArgumentException)
        {
            // Deliberately omit supplied values and inner exceptions from startup diagnostics.
            throw new InvalidOperationException("Employees is empty. Configure valid BootstrapManager FirstName, LastName, Username, Password, Phone and NationalId through User Secrets or environment variables.");
        }
        await repository.AddAsync(employee, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }, cancellationToken);
}
