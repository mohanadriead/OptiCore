using Microsoft.EntityFrameworkCore;
using Npgsql;
using OptiCore.Application.Customers;
using OptiCore.Domain.Customers;

namespace OptiCore.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(OptiCoreDbContext db) : ICustomerRepository
{
    public Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(customer => customer.NationalId == nationalId, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken) =>
        await db.Customers.AddAsync(customer, cancellationToken);

    public Task<Customer?> GetByCustomerNumberAsync(int customerNumber, bool forUpdate, CancellationToken cancellationToken)
    {
        var customers = forUpdate ? db.Customers.AsTracking() : db.Customers.AsNoTracking();
        return customers.SingleOrDefaultAsync(customer => customer.CustomerNumber == customerNumber, cancellationToken);
    }

    public Task<Customer?> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().SingleOrDefaultAsync(customer => customer.NationalId == nationalId, cancellationToken);

    public async Task<IReadOnlyList<Customer>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var hasNumber = int.TryParse(query, out var number);
        // Treat SQL LIKE metacharacters as literal search text.
        var pattern = "%" + query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return await db.Customers.AsNoTracking()
            .Where(customer =>
                (hasNumber && customer.CustomerNumber == number) ||
                customer.NationalId == query ||
                EF.Functions.ILike(customer.MobilePhone, pattern, "\\") ||
                (customer.HomePhone != null && EF.Functions.ILike(customer.HomePhone, pattern, "\\")) ||
                EF.Functions.ILike(customer.FirstName, pattern, "\\") ||
                EF.Functions.ILike(customer.LastName, pattern, "\\") ||
                EF.Functions.ILike(customer.FirstName + " " + customer.LastName, pattern, "\\") ||
                (customer.Email != null && EF.Functions.ILike(customer.Email, pattern, "\\")))
            .OrderBy(customer => customer.CustomerNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Customers_NationalId"
            })
        {
            // The unique index also handles concurrent creates that pass the pre-check.
            throw new DuplicateNationalIdException();
        }
    }
}
