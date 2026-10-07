namespace OptiCore.Application.Employees;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public static class PasswordPolicy
{
    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128)
            throw new ArgumentException("Password must be between 8 and 128 characters.");
    }
}
