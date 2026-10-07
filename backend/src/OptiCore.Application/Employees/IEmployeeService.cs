namespace OptiCore.Application.Employees;

public interface IEmployeeService
{
    Task<EmployeeDto> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> GetCurrentAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, Guid actor, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeDto>> ListAsync(Guid actor, CancellationToken cancellationToken);
    Task<EmployeeDto> GetAsync(int number, Guid actor, CancellationToken cancellationToken);
    Task DeactivateAsync(int number, Guid actor, CancellationToken cancellationToken);
    Task<EmployeeDto> SetManagerStatusAsync(int number, bool isManager, Guid actor, CancellationToken cancellationToken);
    Task ResetPasswordAsync(int number, string newPassword, Guid actor, CancellationToken cancellationToken);
    Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, Guid actor, CancellationToken cancellationToken);
}
