using OptiCore.Domain.Employees;

namespace OptiCore.Application.Employees;

public sealed record CreateEmployeeRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string Phone { get; init; }
    public required string NationalId { get; init; }
    public required bool IsManager { get; init; }
}

public sealed record LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed record ChangeOwnPasswordRequest
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
}

public sealed record ResetEmployeePasswordRequest
{
    public required string NewPassword { get; init; }
}

public sealed record SetManagerStatusRequest
{
    public required bool IsManager { get; init; }
}

public sealed record EmployeeDto(Guid Id, int EmployeeNumber, string FirstName, string LastName,
    string Username, string Phone, string NationalId, bool IsActive, bool IsManager,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc)
{
    public static EmployeeDto FromEmployee(Employee employee) => new(employee.Id, employee.EmployeeNumber,
        employee.FirstName, employee.LastName, employee.Username, employee.Phone, employee.NationalId,
        employee.IsActive, employee.IsManager, employee.CreatedAtUtc, employee.UpdatedAtUtc);
}
