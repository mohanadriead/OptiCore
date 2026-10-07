using OptiCore.Domain.Employees;

namespace OptiCore.Application.Employees;

public sealed class EmployeeService(IEmployeeRepository repository, IPasswordHasher hasher) : IEmployeeService
{
    public async Task<EmployeeDto> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Username);
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            throw new ArgumentException("Password is required and must be at most 128 characters.");
        var employee = await repository.GetByUsernameAsync(Employee.NormalizeUsername(request.Username), cancellationToken);
        if (employee is null || !hasher.Verify(employee.PasswordHash, request.Password))
            throw new InvalidCredentialsException();
        if (!employee.IsActive) throw new InactiveEmployeeException();
        return EmployeeDto.FromEmployee(employee);
    }

    public async Task<EmployeeDto> GetCurrentAsync(Guid id, CancellationToken cancellationToken) =>
        EmployeeDto.FromEmployee(await ActiveAsync(id, false, cancellationToken));

    public Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, Guid actor, CancellationToken cancellationToken) =>
        repository.ExecuteExclusiveAsync(async () =>
        {
            await ManagerAsync(actor, cancellationToken);
            PasswordPolicy.Validate(request.Password);
            var employee = new Employee(request.FirstName, request.LastName, request.Username,
                hasher.Hash(request.Password), request.Phone, request.NationalId, request.IsManager, actor);
            if (await repository.UsernameExistsAsync(employee.NormalizedUsername, cancellationToken))
                throw new DuplicateUsernameException();
            if (await repository.NationalIdExistsAsync(employee.NationalId, cancellationToken))
                throw new DuplicateEmployeeNationalIdException();
            await repository.AddAsync(employee, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return EmployeeDto.FromEmployee(employee);
        }, cancellationToken);

    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(Guid actor, CancellationToken cancellationToken)
    {
        await ManagerAsync(actor, cancellationToken);
        return (await repository.ListAsync(cancellationToken)).Select(EmployeeDto.FromEmployee).ToArray();
    }

    public async Task<EmployeeDto> GetAsync(int number, Guid actor, CancellationToken cancellationToken)
    {
        await ManagerAsync(actor, cancellationToken);
        return EmployeeDto.FromEmployee(await FindAsync(number, false, cancellationToken));
    }

    public Task DeactivateAsync(int number, Guid actor, CancellationToken cancellationToken) =>
        repository.ExecuteExclusiveAsync(async () =>
        {
            await ManagerAsync(actor, cancellationToken);
            var employee = await FindAsync(number, true, cancellationToken);
            await GuardRemovalAsync(employee, cancellationToken);
            employee.Deactivate(actor);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task<EmployeeDto> SetManagerStatusAsync(int number, bool isManager, Guid actor, CancellationToken cancellationToken) =>
        repository.ExecuteExclusiveAsync(async () =>
        {
            await ManagerAsync(actor, cancellationToken);
            var employee = await FindAsync(number, true, cancellationToken);
            if (!isManager) await GuardRemovalAsync(employee, cancellationToken);
            employee.SetManagerStatus(isManager, actor);
            await repository.SaveChangesAsync(cancellationToken);
            return EmployeeDto.FromEmployee(employee);
        }, cancellationToken);

    public Task ResetPasswordAsync(int number, string newPassword, Guid actor, CancellationToken cancellationToken) =>
        repository.ExecuteExclusiveAsync(async () =>
        {
            await ManagerAsync(actor, cancellationToken);
            PasswordPolicy.Validate(newPassword);
            var employee = await FindAsync(number, true, cancellationToken);
            employee.ChangePasswordHash(hasher.Hash(newPassword), actor);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, Guid actor, CancellationToken cancellationToken) =>
        repository.ExecuteExclusiveAsync(async () =>
        {
            PasswordPolicy.Validate(request.NewPassword);
            var employee = await ActiveAsync(actor, true, cancellationToken);
            if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128 ||
                !hasher.Verify(employee.PasswordHash, request.CurrentPassword))
                throw new InvalidCredentialsException();
            employee.ChangePasswordHash(hasher.Hash(request.NewPassword), actor);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    private async Task GuardRemovalAsync(Employee employee, CancellationToken cancellationToken)
    {
        if (employee.IsActive && employee.IsManager && await repository.CountActiveManagersAsync(cancellationToken) <= 1)
            throw new LastActiveManagerException();
    }

    private async Task<Employee> ActiveAsync(Guid id, bool forUpdate, CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(id, forUpdate, cancellationToken) ?? throw new InvalidCredentialsException();
        if (!employee.IsActive) throw new InactiveEmployeeException();
        return employee;
    }

    private async Task ManagerAsync(Guid actor, CancellationToken cancellationToken)
    {
        if (!(await ActiveAsync(actor, false, cancellationToken)).IsManager) throw new ManagerRequiredException();
    }

    private async Task<Employee> FindAsync(int number, bool forUpdate, CancellationToken cancellationToken) =>
        await repository.GetByNumberAsync(number, forUpdate, cancellationToken) ?? throw new EmployeeNotFoundException();
}
