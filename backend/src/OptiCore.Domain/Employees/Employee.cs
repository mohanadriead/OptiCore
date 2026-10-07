using OptiCore.Domain.Common;

namespace OptiCore.Domain.Employees;

public sealed class Employee : AuditableEntity
{
    public int EmployeeNumber { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string NationalId { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsManager { get; private set; }

    private Employee() { }

    public Employee(string firstName, string lastName, string username, string passwordHash,
        string phone, string nationalId, bool isManager, Guid? createdByEmployeeId)
        : base(createdByEmployeeId)
    {
        FirstName = Required(firstName, 100, "First name");
        LastName = Required(lastName, 100, "Last name");
        Username = Required(username, 100, "Username");
        NormalizedUsername = NormalizeUsername(Username);
        PasswordHash = Required(passwordHash, 1024, "Password hash");
        Phone = Required(phone, 30, "Phone");
        NationalId = Required(nationalId, 9, "National ID");
        IsManager = isManager;
    }

    public static string NormalizeUsername(string username) =>
        Required(username, 100, "Username").ToUpperInvariant();

    public void ChangePasswordHash(string passwordHash, Guid employeeId)
    {
        PasswordHash = Required(passwordHash, 1024, "Password hash");
        MarkUpdated(employeeId);
    }

    public void Deactivate(Guid employeeId)
    {
        if (!IsActive) return;
        IsActive = false;
        MarkUpdated(employeeId);
    }

    // The cross-employee last-manager invariant is enforced transactionally by the application service.
    public void SetManagerStatus(bool isManager, Guid employeeId)
    {
        if (IsManager == isManager) return;
        IsManager = isManager;
        MarkUpdated(employeeId);
    }

    private static string Required(string? value, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(field + " is required.");
        if (value.Trim().Length > maximum)
            throw new ArgumentException(field + " must be at most " + maximum + " characters.");
        return value.Trim();
    }
}
