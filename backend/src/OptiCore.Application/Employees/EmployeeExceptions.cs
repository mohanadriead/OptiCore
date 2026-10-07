namespace OptiCore.Application.Employees;

public sealed class EmployeeNotFoundException() : Exception("Employee was not found.");
public sealed class DuplicateUsernameException() : Exception("Username already exists.");
public sealed class DuplicateEmployeeNationalIdException() : Exception("An employee with this NationalId already exists.");
public sealed class LastActiveManagerException() : Exception("At least one active manager must remain.");
public sealed class InvalidCredentialsException() : Exception("Invalid username or password.");
public sealed class InactiveEmployeeException() : Exception("Employee is inactive.");
public sealed class ManagerRequiredException() : Exception("Manager authorization is required.");
