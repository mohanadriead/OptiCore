using Microsoft.AspNetCore.Identity;
using OptiCore.Application.Employees;

namespace OptiCore.Infrastructure.Security;

public sealed class EmployeePasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> hasher = new();
    private static readonly object User = new();

    public string Hash(string password) => hasher.HashPassword(User, password);

    public bool Verify(string passwordHash, string password)
    {
        try { return hasher.VerifyHashedPassword(User, passwordHash, password) != PasswordVerificationResult.Failed; }
        catch (FormatException) { return false; }
    }
}
